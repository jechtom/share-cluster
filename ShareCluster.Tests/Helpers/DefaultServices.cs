using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Tests.Helpers
{
    public class DefaultServices
    {
        public static DefaultServices Default = new DefaultServices();
        public static ILoggerFactory DefaultLoggerFactory => Default.LoggerFactory;
        public static CryptoProvider DefaultCrypto => Default.CryptoProvider;
        
        public ILoggerFactory LoggerFactory { get; } =
                new LoggerFactory().AddTraceSource("Test");

        public CryptoProvider CryptoProvider { get; } =
                AppInfo.CreateDefaultCryptoProvider();
    }
}
