using Microsoft.Extensions.Logging;
using ShareCluster.Packaging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ShareCluster.Tests
{
    public class LongRunningTasksTests
    {
        [Fact]
        public void EmptyManager()
        {
            var loggerFactory = new LoggerFactory();
            var tasksManager = new LongRunningTasksManager(loggerFactory);
            Assert.Equal(0, tasksManager.Tasks.Count);
        }
    }
}
