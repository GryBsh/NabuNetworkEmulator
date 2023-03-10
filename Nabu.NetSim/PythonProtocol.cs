using Microsoft.Extensions.Hosting;
using Python.Runtime;
using Nabu.Adaptor;
using Python.Deployment;
using static System.Formats.Asn1.AsnWriter;
using System;
using Nabu.Patching;
using Nabu.Network;
using Nabu.Services;

namespace Nabu;

public class PythonProtocol : Protocol
{
    ProtocolSettings Protocol { get; }
    
    public PythonProtocol(IConsole<PythonProtocol> logger, ProtocolSettings protoSettings) : base(logger)
    {
        Protocol = protoSettings;
        Commands = Protocol.Commands;
        
    }

    public override byte[] Commands { get; }
    public override byte Version { get; } = 0x01;


    protected override async Task Handle(byte unhandled, CancellationToken cancel)
    {
        var proxy = new ProxyProtocol(Logger);
        proxy.Attach(Settings, Stream);
        string source = await File.ReadAllTextAsync(Protocol.Path, cancel);
        await Task.Run(() =>
        {
            using var state = Py.GIL();
            using PyModule scope = Py.CreateScope();
            try
            {
                scope.Set("incoming", unhandled.ToPython());
                scope.Set("adaptor", proxy.ToPython());
                scope.Set("logger", Logger.ToPython());
                scope.Exec(source);
            }
            catch (Exception ex)
            {
                Logger.WriteError(ex.Message, ex);
            }

        }, cancel);
    }

  
    /*
    protected static async Task EnsurePython(IConsole logger, string pythonLib)
    {
        Installer.LogMessage += logger.Write;
        var python_installed = Installer.IsPythonInstalled();
        var pip_installed = Installer.IsPipInstalled();

        if (!python_installed) await Installer.SetupPython();
        if (!pip_installed) await Installer.InstallPip();
        Runtime.PythonDLL = Path.Join(Installer.EmbeddedPythonHome, pythonLib);
    }
    */

    public static void Startup(IConsole logger)
    {
        logger.Write($"Starting Python from {Runtime.PythonDLL}");
        PythonEngine.Initialize();
        PythonEngine.BeginAllowThreads();
    }

}

