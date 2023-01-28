﻿using Microsoft.Extensions.Logging;
using Nabu.Network;
using Nabu.Patching;
using System.Collections;
using System.Collections.Specialized;
using System.Reactive.Linq;

namespace Nabu;

public class FileSystemObserver : IEnumerable<NabuProgram>, INotifyCollectionChanged
{
    ProgramSource Source { get; }
    FileSystemWatcher Watcher { get; }
    List<NabuProgram> _programs;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public IEnumerable<NabuProgram> Programs => _programs;
    public IObservable<FileSystemEventArgs> Changed { get; }
       
    ILogger Logger { get; }

    public FileSystemObserver(ILogger logger, ProgramSource source, IEnumerable<NabuProgram>? programs = null)
    {
        Logger = logger;
        Source = source;
        _programs = new(Refresh());

        if (Source.SourceType is SourceType.Remote or SourceType.Unknown)
        {
            Watcher = new();
            Changed = Observable.Empty<FileSystemEventArgs>();
            return;
        }
        
        Watcher = new(source.Path)
        {
            IncludeSubdirectories = false
        };
        
        Changed = Observable.Merge(
            Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => Watcher.Created += h,
                h => Watcher.Created -= h
            ).Select(x => x.EventArgs),
            Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => Watcher.Changed += h,
                h => Watcher.Changed -= h
            ).Select(x => x.EventArgs),
            Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => Watcher.Deleted += h,
                h => Watcher.Deleted -= h
            ).Select(x => x.EventArgs),
            Observable.FromEventPattern<RenamedEventHandler, FileSystemEventArgs>(
                h => Watcher.Renamed+= h,
                h => Watcher.Renamed -= h
            ).Select(x => x.EventArgs),
            Observable.FromEventPattern<ErrorEventHandler, FileSystemEventArgs>(
                h => Watcher.Error += h,
                h => Watcher.Error -= h
            ).Select(x => x.EventArgs)
        );
        Changed.Subscribe(e =>
        {
            _programs = new(Refresh());
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        });
        Watcher.EnableRaisingEvents = true;
    }

    public void Add(NabuProgram program)
    {
        _programs.Add(program);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
    }

    public IEnumerable<NabuProgram> Refresh()
    {
        if (Source.Path is null) yield break;

        var files = Directory.GetFiles(Source.Path, "*.npak");
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            yield return new(
                name,
                name,
                Source.Name,
                DefinitionType.Folder,
                file,
                SourceType.Local,
                ImageType.Pak,
                new[] { new PassThroughPatch(Logger) }
            );
        }

        files = Directory.GetFiles(Source.Path, "*.nabu");
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            yield return new(
                name,
                name,
                Source.Name,
                DefinitionType.Folder,
                file,
                SourceType.Local,
                ImageType.Raw,
                new[] { new PassThroughPatch(Logger) }
            );
        }
    }

    public IEnumerator<NabuProgram> GetEnumerator()
    {
        return Programs.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Programs).GetEnumerator();
    }
}

