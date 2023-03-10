using Nabu.Services;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Nabu.Network;

public class CachingHttpClient : IHttpCache
{
    HttpClient Http { get; }
    IConsole Logger { get; }
    
    protected static string CacheFolder => Path.Join(AppContext.BaseDirectory, "cache");
    protected FileCache MemoryCache { get; }
    readonly Settings Settings;

    public CachingHttpClient(HttpClient http, IConsole logger, FileCache cache, Settings settings)
    {
        Http = http;
        Logger = logger;
        MemoryCache = cache;
        Settings = settings;
        Task.Run(() => NabuLib.EnsureFolder(CacheFolder));
    }

    public async Task<HttpResponseMessage> GetHead(string uri)
    {
        try
        {
            return await Http.SendAsync(new(HttpMethod.Head, uri));
        }
        catch
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    public async Task<(bool, bool, bool, DateTime)> GetUriStatus(string uri, string? path = null)
    {
        var head = await GetHead(uri);
        path ??= Path.Join(CacheFolder, NabuLib.SafeFileName(uri));
        var pathExists = File.Exists(path);
        
        if (!pathExists)
        {
            if (!head.IsSuccessStatusCode)
            {
                return (false, false, false, DateTime.Now); //No download, not found, no local
            }
            return (true, true, false, DateTime.MinValue); //Download, Found, None
        }

        var modified = head.Content.Headers.LastModified;
        var lastCached = MemoryCache.LastChange(path);

        if (modified > lastCached)
        {
            return (true, true, true, DateTime.MinValue);
        }
        return (false, true, true, lastCached);
    }

    public async Task<byte[]> GetBytes(string uri)
    {
        var safeName = NabuLib.SafeFileName(uri);
        var path = Path.Join(CacheFolder, safeName);
        var name = Path.GetFileName(uri);

        var (shouldDownload, found, local, last) = await GetUriStatus(uri, path);

        if (!shouldDownload && !found && !local)
            return Array.Empty<byte>();

        if (shouldDownload && found)
        {
            Logger.Write($"Downloading {uri}");
            var bytes = await Http.GetByteArrayAsync(uri);

            Logger.Write($"Writing {bytes.Length} bytes to {name}");
            try
            {
                //await File.WriteAllBytesAsync(path, bytes);
                MemoryCache.CacheFile(path, bytes, true);
            }
            catch
            {
                Logger.WriteWarning("Caching failed, please try again later");
            }
            return bytes;
        }

        Logger.Write($"Reading {name} from cache");
        return await MemoryCache.GetFile(path);
    }

    

    public async Task<string> GetString(string uri)
    {
        var safeName = NabuLib.SafeFileName(uri);
        var path = Path.Join(CacheFolder, safeName);
        var name = Path.GetFileName(uri);
        var (shouldDownload, found, local, _) = await GetUriStatus(uri, path);

        if (!shouldDownload && !found && !local) return string.Empty;

        if (shouldDownload && found)
        {
            Logger.Write($"Downloading {uri}");
            var str = await Http.GetStringAsync(uri);

            Logger.Write($"Writing {str.Length} characters to {name} in cache");
            try
            {
                //await File.WriteAllTextAsync(path, str);
                MemoryCache.CacheString(path, str, true);
            }
            catch
            {
                Logger.WriteWarning("Caching failed, please try again later");
            }
            
            return str;
        }
        
        Logger.Write($"Reading {name} from cache");
        return await MemoryCache.GetString(path);

    }

}
