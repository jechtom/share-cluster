using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Tests.Helpers
{
    public class TestLoggerFactory
    {
        public static TestLoggerFactory Default = new TestLoggerFactory();
        public static ILoggerFactory DefaultFactory => Default.LoggerFactory;

        public TestLoggerFactory()
        {
            LoggerFactory =
                new LoggerFactory()
                .AddTraceSource("Test");
        }

        public ILoggerFactory LoggerFactory { get; }
    }
}
