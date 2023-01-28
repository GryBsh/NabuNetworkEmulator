﻿using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using System;

namespace Nabu.Network.RetroNet;

public class RetroNetHttpHandler : NabuService, IRetroNetFileHandler
{
    public RetroNetHttpHandler(ILogger logger, HttpClient client, AdaptorSettings settings) : base(logger, settings)
    {
        Client = client;
    }
    public HttpClient Client { get; }

    public async Task<byte[]> Get(string filename, CancellationToken cancel)
    {
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(filename, cancel);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancel);
            }
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            return Array.Empty<byte>();
        }
    }

    public Task Copy(string source, string destination, CopyMoveFlags flags)
    {
        throw new NotImplementedException();
    }

    public Task Delete(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<FileDetails> FileDetails(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<FileDetails> Item(short index)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<FileDetails>> List(string path, string wildcard, FileListFlags flags)
    {
        throw new NotImplementedException();
    }

    public Task Move(string source, string destination, CopyMoveFlags flags)
    {
        throw new NotImplementedException();
    }

    public async Task<int> Size(string filename)
    {
        var head = await Client.SendAsync(new(HttpMethod.Head, filename));
        var size = (int?)head.Content.Headers.ContentLength ?? -1;
        Log($"HTTP: {filename}:{size}");
        return size;
    }
}
