﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nabu.Network;
using System.Net.Sockets;
using System.Net;
using Nabu.ACP;

namespace Nabu.Adaptor;

public class TCPAdaptor
{
    public static async Task Start(IServiceProvider serviceProvider, ILogger logger, AdaptorSettings settings, CancellationToken stopping)
    {
        var socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        if (!int.TryParse(settings.Port, out int port))
        {
            port = Constants.DefaultTCPPort;
        };

        settings.SendDelay = settings.SendDelay ?? Constants.DefaultTCPSendDelay;

        logger.LogInformation(
            $"\n\t Port: {port}" +
            $"\n\t SendDelay: {settings.SendDelay}"
        );

        while (socket.Connected is false)
            try
            {
                socket.Connect(
                    new IPEndPoint(IPAddress.Loopback, port)
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning($"TCP Adaptor: {ex.Message}");
                Thread.Sleep(5000);
            }

        var stream = new NetworkStream(socket);
        var adaptor = new AdaptorEmulator(
             settings,
             serviceProvider.GetRequiredService<NabuNetProtocol>(),
             //ServiceProvider.GetServices<IProtocolExtension>(),
             serviceProvider.GetRequiredService<ACPProtocol>(),
             serviceProvider.GetRequiredService<ILogger<TCPAdaptor>>(),
             stream
         );

        await adaptor.Emulate(stopping);
        stream.Dispose();
        socket.Close();
    }
}