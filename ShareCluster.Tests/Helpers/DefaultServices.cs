using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.TraceSource;

namespace ShareCluster.Tests.Helpers
{
    public class DefaultServices
    {
        public static DefaultServices Default = new DefaultServices();
        public static ILoggerFactory DefaultLoggerFactory => Default.LoggerFactory;
        public static CryptoProvider DefaultCrypto => Default.CryptoProvider;
        
        public ILoggerFactory LoggerFactory { get; } =
            new LoggerFactory(new[] { new TraceSourceLoggerProvider(new SourceSwitch("Test")) });

        public CryptoProvider CryptoProvider { get; } =
                AppInfo.CreateDefaultCryptoProvider();
    }
}
