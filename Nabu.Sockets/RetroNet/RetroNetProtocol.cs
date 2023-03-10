using Microsoft.Extensions.Logging;
using Nabu.Adaptor;
using Nabu.Network.RetroNetHandle;
using System.Net.Cache;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Nabu.Services;

namespace Nabu.Network.RetroNet;

public partial class RetroNetProtocol : Protocol
{
    Dictionary<byte, IRetroNetFileHandle> Slots { get; } = new();
    FileDetails[]? CurrentList { get; set; }
    HttpClient HttpClient { get; }
    FileCache FileCache { get; }
    INabuNetwork NabuNet { get; }
    Dictionary<string, byte[]> Cache { get; } = new();
    readonly Settings Global;
    
    byte NextIndex()
    {
        for (int i = 0x00; i < 0xFF; i++)
        {
            if (Slots.ContainsKey((byte)i)) continue;
            return (byte)i;
        }
        return 0xFF;
    }


    public RetroNetProtocol(
        IConsole<RetroNetProtocol> logger,
        HttpClient httpClient,
        INabuNetwork nabuNet,
        FileCache cache,
        Settings settings
    ) : base(logger)
    {
        HttpClient = httpClient;
        NabuNet = nabuNet;
        FileCache = cache;
        Global = settings;
    }

    public override byte[] Commands { get; } = new byte[] {
        RetroNetCommands.FileCopy,
        RetroNetCommands.FileDelete,
        RetroNetCommands.FileHandleAppend,
        RetroNetCommands.FileHandleClose,
        RetroNetCommands.FileHandleDeleteRange,
        RetroNetCommands.FileHandleDetails,
        RetroNetCommands.FileHandleInsert,
        RetroNetCommands.FileHandleRead,
        RetroNetCommands.FileHandleReadSequence,
        RetroNetCommands.FileHandleReplaceRange,
        RetroNetCommands.FileHandleSeek,
        RetroNetCommands.FileHandleSize,
        RetroNetCommands.FileHandleTruncate,
        RetroNetCommands.FileIndexStat,
        RetroNetCommands.FileList,
        RetroNetCommands.FileMove,
        RetroNetCommands.FileOpen,
        RetroNetCommands.FileSize,
        RetroNetCommands.FileStat
    };

    public override byte Version { get; } = 0x01;

    protected override Task Handle(byte unhandled, CancellationToken cancel)
    {
        try
        {
            switch (unhandled)
            {
                case RetroNetCommands.FileOpen:
                    return FileOpen(cancel);
                case RetroNetCommands.FileHandleSize:
                    return FileHandleSize(cancel);
                case RetroNetCommands.FileHandleRead:
                    return FileHandleRead(cancel);
                case RetroNetCommands.FileHandleClose:
                    return FileHandleClose(cancel);
                case RetroNetCommands.FileSize:
                    return FileSize(cancel);
                case RetroNetCommands.FileHandleAppend:
                    return FileHandleAppend(cancel);
                case RetroNetCommands.FileHandleInsert:
                    return FileHandleInsert(cancel);
                case RetroNetCommands.FileHandleDeleteRange:
                    return FileHandleDelete(cancel);
                case RetroNetCommands.FileHandleReplaceRange:
                    return FileHandleReplace(cancel);
                case RetroNetCommands.FileDelete:
                    return FileDelete(cancel);
                case RetroNetCommands.FileCopy:
                    return FileCopy(cancel);
                case RetroNetCommands.FileMove:
                    return FileMove(cancel);
                case RetroNetCommands.FileHandleTruncate:
                    return FileHandleTruncate(cancel);
                case RetroNetCommands.FileList:
                    return FileList(cancel);
                case RetroNetCommands.FileIndexStat:
                    return FileIndexStat(cancel);
                case RetroNetCommands.FileStat:
                    return FileStat(cancel);
                case RetroNetCommands.FileHandleDetails:
                    return FileHandleDetails(cancel);
                case RetroNetCommands.FileHandleReadSequence:
                    return FileHandleReadSequence(cancel);
                case RetroNetCommands.FileHandleSeek:
                    return FileHandleSeek(cancel);
                default:
                    Warning($"Unsupported message: {Format(unhandled)}");
                    return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            return Task.CompletedTask;
        }
    }

    void WriteBuffer(Span<byte> bytes)
    {
        Writer.Write(NabuLib.FromShort((short)bytes.Length));
        Writer.Write(bytes);
    }
    
    IRetroNetFileHandler FileHandler(string filename)
    {
        return filename switch
        {
            _ when Http().IsMatch(filename) => new RetroNetHttpHandler(Logger, HttpClient, Settings, FileCache, Global),
            _ => new RetroNetFileHandler(Logger, Settings)
        };
    }

    private async Task FileStat(CancellationToken cancel)
    {
        var filename = RecvString();
        var details = await FileHandler(filename).FileDetails(filename);
        Log($"Details: {filename}");
        Writer.Write(details);
    }

    private async Task FileList(CancellationToken cancel)
    {
        var path = RecvString();
        var wildcard = RecvString();
        var flags = (FileListFlags)Recv();
        Log($"List: {path}\\{wildcard}");
        CurrentList = (await FileHandler(path).List(path, wildcard, flags)).ToArray();
        Writer.Write((short)CurrentList.Count());
    }

    private Task FileIndexStat(CancellationToken cancel)
    {
        var index = RecvShort();
        var file = CurrentList![index];
        Log($"List Details: {index}");
        Writer.Write(file);
        return Task.CompletedTask;
    }

    private async Task FileMove(CancellationToken cancel)
    {
        var source = RecvString();
        var destination = RecvString();
        var flags = (CopyMoveFlags)Recv();
        Log($"Move: {source} -> {destination}");
        await FileHandler(source).Move(source, destination, flags);
    }

    private async Task FileCopy(CancellationToken cancel)
    {
        var source = RecvString();
        var destination = RecvString();
        var flags = (CopyMoveFlags)Recv();
        Log($"Copy: {source} -> {destination}");
        await FileHandler(source).Copy(source, destination, flags);
    }

    private async Task FileDelete(CancellationToken cancel)
    {
        var filename = RecvString();
        await FileHandler(filename).Delete(filename);
        Log($"Deleted: {filename}");
    }

    private async Task FileSize(CancellationToken cancel)
    {
        var filename = RecvString();
        //var handle = NextIndex();
        //await FileOpen(filename, FileOpenFlags.ReadOnly, handle, cancel);
        Log($"Size: {filename}");

        var size = Cache.ContainsKey(filename) switch
        {
            true => Cache[filename].Length,
            false => await FileHandler(filename).Size(filename)
        };
        Writer.Write(size);
    }

    public override void Reset()
    {
        if (Slots.Count > 0 || Cache.Count > 0)
        {
            var cancel = CancellationToken.None;
            if (Slots.Count > 0)
            {
                foreach (var b in Slots.Keys) Slots[b].Close(cancel);
                Slots.Clear();
            }
            if (Cache.Count > 0)
            {
                Cache.Clear();
            }
            base.Reset();
        }
    }

    public override bool ShouldAccept(byte unhandled)
    {
        var command = base.ShouldAccept(unhandled);
        var source = NabuNet.Source(Settings);
        var enabled = source.EnableRetroNet;

        return command && enabled;
    }

    [GeneratedRegex("http[s]?://.*")]
    private static partial Regex Http();

    [GeneratedRegex("ftp://.*")]
    private static partial Regex Ftp();

    [GeneratedRegex("0x//.*")]
    private static partial Regex Memory();
}

