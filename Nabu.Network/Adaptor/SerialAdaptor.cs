using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nabu.Network;
using Nabu.Services;
using System.IO.Ports;

namespace Nabu.Adaptor;

public class SerialAdaptor
{
    private SerialAdaptor() { }
    public static async Task Start(
        IServiceProvider serviceProvider, 
        SerialAdaptorSettings settings, 
        CancellationToken stopping
    ) {
        var logger = serviceProvider.GetRequiredService<IConsole<SerialAdaptor>>();

        var serial = new SerialPort(
            settings.Port,
            settings.BaudRate, //115200
            Parity.None,
            8,
            StopBits.Two
        )
        {
            ReceivedBytesThreshold = 1,
            Handshake = Handshake.None,
            RtsEnable = true,
            DtrEnable = true,
            ReadBufferSize = 2,
            WriteBufferSize = 2,
        };

        if (settings.ReadTimeout > 0)
        {
            serial.ReadTimeout = settings.ReadTimeout;
        }

        logger.Write(
            $"Port: {settings.Port}, BaudRate: {settings.BaudRate}, ReadTimeout: {settings.ReadTimeout}"
        );

        while (serial.IsOpen is false)
            try
            {
                serial.Open();
            }
            catch (UnauthorizedAccessException)
            {
                logger.WriteError($"Serial Adaptor: Port {settings.Port} is inaccessible");
                settings.Next = ServiceShould.Stop;
                break;
            }
            catch (Exception ex)
            {
                logger.WriteWarning($"Serial Adaptor: {ex.Message}");
                Thread.Sleep(5000);
            }

        if (serial.IsOpen is false)
        {
            serial.Close();
            serial.Dispose();
            return;
        }

        try
        {
            var adaptor = new EmulatedAdaptor(
                settings,
                serviceProvider.GetRequiredService<ClassicNabuProtocol>(),
                serviceProvider.GetServices<IProtocol>(),
                logger,
                serial.BaseStream
            );
            logger.Write($"Adaptor Started");
            await adaptor.Listen(stopping);

        }
        
        catch (Exception ex)
        {
            logger.WriteError(ex.Message, ex);
        }

        serial.Close();
        serial.Dispose();

    }

}
