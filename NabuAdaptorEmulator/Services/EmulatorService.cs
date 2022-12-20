﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nabu.Adaptor;
using Nabu.Binary;

namespace Nabu.Services;

public class EmulatorService : BackgroundService
{
    readonly ILogger Logger;
    readonly SerialAdaptorEmulator Serial;
    readonly TCPAdaptorEmulator TCP;
    readonly ServiceSettings Settings;

    public EmulatorService(
        ILogger<EmulatorService> logger,
        ServiceSettings settings,
        SerialAdaptorEmulator adaptor,
        TCPAdaptorEmulator tcp
    )
    {
        Logger = logger;
        Settings = settings;
        Serial = adaptor;
        TCP = tcp;
    }

    
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await Task.Run(() => {
            Task serial = Task.CompletedTask, 
                tcp = Task.CompletedTask;

            while (stopping.IsCancellationRequested is false)
            {
                Thread.Sleep(1000);
                if (Settings.Serial && serial.IsCompleted)
                    serial = NabuService.From(
                        Serial.Emulate,
                        stopping,
                        () => Serial.Open(),
                        () => Serial.Close()
                    );

                if (Settings.TCP && tcp.IsCompleted)
                    tcp = NabuService.From(
                        TCP.Emulate,
                        stopping,
                        () => TCP.Open(),
                        () => TCP.Close()
                    );
            }
        }, stopping);
    }
}


