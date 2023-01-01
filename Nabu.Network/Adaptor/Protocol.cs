﻿using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace Nabu.Adaptor;

public abstract class Protocol : NabuService, IProtocol
{
    protected AdaptorSettings Settings { get; private set; }
    protected Stream Stream { get; private set; } = Stream.Null;
    protected BinaryReader Reader { get; private set; }
    protected BinaryWriter Writer { get; private set; }
    protected abstract byte Version { get; }
    public abstract byte Identifier { get; }
    public bool Attached => Stream != Stream.Null;
    
    int SendDelay = 0;
    protected Protocol(
        ILogger logger
    ) : base(logger)
    {
        Settings = new();
        Reader = new BinaryReader(Stream, Encoding.ASCII);
        Writer = new BinaryWriter(Stream, Encoding.ASCII);
        
    }


    #region Send / Receive
    // These methods perform all the stream reading / writing
    // for all communication with the NABU PC / Emulator
    protected byte Recv()
    {
        var b = Reader.ReadByte();
        Trace($"NA: RCVD: {Format(b)}");
        Debug($"NA: RCVD: 1 byte");
        return b;
    }

    bool Expected(byte expected, byte read)
    {
        var good = read == expected;
        if (!good)
            Warning($"NA: {Format(expected)} != {Format(read)}");
        return good;
    }

    protected (bool, byte) Recv(byte byt)
    {
        var read = Recv();
        var expected = Expected(byt, read);
        return (expected, read);
    }

    protected byte[] Recv(int length = 1)
    {
        var buffer = Reader.ReadBytes(length);
        Trace($"NA: RCVD: {FormatSeperated(buffer)}");
        Debug($"NA: RCVD: {buffer.Length} bytes");
        return buffer;
    }

    bool Expected(byte[] expected, byte[] read)
    {
        var good = expected.SequenceEqual(read);
        if (!good)
            Warning($"NA: {FormatSeperated(expected)} != {FormatSeperated(read)}");
        return good;
    }

    protected (bool, byte[]) Recv(params byte[] bytes)
    {
        var read = Recv(bytes.Length);
        bool expected = Expected(bytes, read);
        return (expected, read);
    }

    void TransferRate(DateTime start, DateTime stop, int length)
    {
        var elapsed = stop - start;
        var rate = 8 * length / elapsed.TotalSeconds / 1000;
        var unit = "Kbps";
        if (rate > 1000)
        {
            rate = rate / 1000;
            unit = "Mbps";
        }
        Log($"NPC: Transfer Rate: {rate:0.00} {unit}");
    }

    protected void Send(params byte[] bytes)
    {
        Trace($"NA: SEND: {FormatSeperated(bytes)}");
        if (bytes.Length > 128) { // This doesn't work unless you fill the buffer
            DateTime start = DateTime.Now;
            Writer.Write(bytes);
            DateTime end = DateTime.Now;
            Task.Run(() => TransferRate(start, end, bytes.Length));
        }
        else Writer.Write(bytes);
        Debug($"NA: SENT: {bytes.Length} bytes");
    }


    protected void SlowerSend(params byte[] bytes)
    {
        Trace($"NA: SEND: {FormatSeperated(bytes)}");
        for (int i = 0; i < bytes.Length; i++)
        {
            Writer.Write(bytes[i]);
            Thread.SpinWait(SendDelay);
        }
        Debug($"NA: SENT: {bytes.Length} bytes");
    }

    #endregion

    #region Framed Protocols
    protected void SendFramed(params byte[] buffer)
    {
        Send(NabuLib.FromShort((short)buffer.Length));
        Send(buffer);
    }

    protected void SendFramed(byte header, params IEnumerable<byte>[] buffer)
    {
        var head = new byte[] { header };
        var wad = NabuLib.Concat(buffer).ToArray();
        if (wad.Length is 0) SendFramed(head);   // Empty frame     
        else SendFramed(NabuLib.Concat(head, wad).ToArray());
    }

    protected (short, byte[]) ReadFrame()
    {
        var length  = NabuLib.ToShort(Recv(2));
        if (0 > length)
        {
            Warning($"NabuNet message detected in frame, aborting.");
            return (0, Array.Empty<byte>());
        }
        else if (length is 0)
        {
            Warning($"0 length frame, aborting.");
            return (0, Array.Empty<byte>());
        }
        var buffer = Recv(length);
        return (length, buffer);
    }

    #endregion

    #region IProtocol

    public virtual bool Attach(AdaptorSettings settings, Stream stream)
    {
        if (Attached) return false;
        else if (
            stream.CanRead is false &&
            stream.CanWrite is false
        ) return false;

        Stream = stream;
        Settings = settings;
        SendDelay = settings.SendDelay ?? 0;
        Reader = new BinaryReader(Stream, Encoding.ASCII);
        Writer = new BinaryWriter(Stream, Encoding.ASCII);
        return true;
    }

    public abstract void OnListen();
    public abstract Task<bool> Listen(byte unhandled);
    
    public async Task<bool> Listen(CancellationToken cancel, byte incoming)
    {
        OnListen();
        Debug($"v{Version} Running...");
        
        try
        {
            while (await Listen(incoming))
                continue;

            Debug($"End {Version}");
            return true;
        }
        catch (TimeoutException) {
            return true;
        }
        catch (Exception ex)
        {
            Error($"FAIL v{Version}: {ex.Message}");
            return false;
        }

    }

    public virtual void Detach()
    {
        Settings = new();
        Stream = Stream.Null;
        Reader = new BinaryReader(Stream);
        Writer = new BinaryWriter(Stream);
    }

    #endregion
}

