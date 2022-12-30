﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nabu.ACP;
using Nabu.Network;
using Nabu.Services;

var builder = Host.CreateDefaultBuilder(args);


builder.ConfigureServices(
    (context, services) =>
    {
        var settings = new Settings();
        context.Configuration.Bind("Settings", settings);
        services.AddSingleton(settings);
        services.AddSingleton(settings.Sources);
        services.AddLogging(
            logging =>

                logging.AddSimpleConsole(
                    options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                    }
                )
        );

        services.AddHttpClient();
        services.AddTransient<NetworkEmulator>();
        services.AddTransient<NabuNetProtocol>();
        services.AddTransient<ACPProtocol>();
        services.AddHostedService<EmulatorService>();
    }
);



//var tokenSource = new CancellationTokenSource();
await builder.RunConsoleAsync();
