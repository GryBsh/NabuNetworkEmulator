﻿using Microsoft.Extensions.Logging;
using Nabu.Binary;
using Nabu.Network;

namespace Nabu.Adaptor;

public class SerialAdaptorEmulator : AdaptorEmulator
{
    public SerialAdaptorEmulator(
        NetworkEmulator network, 
        ILogger<SerialAdaptorEmulator> logger, 
        AdaptorSettings settings,
        SerialAdapterSettings adapterSettings
    ) : base(
        network, 
        logger, 
        settings, 
        new SerialAdapter(adapterSettings, logger)
    ) {}
}