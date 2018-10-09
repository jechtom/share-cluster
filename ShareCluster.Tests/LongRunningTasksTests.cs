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
            Assert.Equal(0, tasksManager.CompletedTasks.Count);
            Assert.Equal(0, tasksManager.Tasks.Count);
        }

        [Fact]
        public void SingleTaskSuccess()
        {
            var loggerFactory = new LoggerFactory();
            var tasksManager = new LongRunningTasksManager(loggerFactory);
            LongRunningTask task;

            tasksManager.AddTaskToQueue(task = new LongRunningTask("Test", Task.Delay(TimeSpan.FromMilliseconds(100))));

            // started but not finished
            Assert.Contains(task, tasksManager.Tasks);
            Assert.DoesNotContain(task, tasksManager.CompletedTasks);

            task.CompletionTask.Wait();
            Thread.Sleep(50);

            // finisihed
            Assert.True(task.IsCompletedSuccessfully);
            Assert.DoesNotContain(task, tasksManager.Tasks);
            Assert.Contains(task, tasksManager.CompletedTasks);
        }

        [Fact]
        public void SingleTaskFailProgress()
        {
            var loggerFactory = new LoggerFactory();
            var tasksManager = new LongRunningTasksManager(loggerFactory);
            Exception exc = new Exception("test exception");
            LongRunningTask task = new LongRunningTask("Test", Task.Run(new Action(() => throw exc)));

            tasksManager.AddTaskToQueue(task);

            task.CompletionTask.Wait();
            Thread.Sleep(50);

            Assert.False(task.IsCompletedSuccessfully);
            Assert.True(task.IsFaulted);
            Assert.True(task.IsCompleted);
            Assert.Equal(exc, task.FaultException);
            Assert.DoesNotContain(task, tasksManager.Tasks);
            Assert.Contains(task, tasksManager.CompletedTasks);
        }
        
        [Fact]
        public void SingleTaskAlreadyStarted()
        {
            var loggerFactory = new LoggerFactory();
            var tasksManager = new LongRunningTasksManager(loggerFactory);

            var task = new LongRunningTask("Test", Task.CompletedTask);
            tasksManager.AddTaskToQueue(task); // should be placed directly to finished 
            Thread.Sleep(50);

            Assert.True(task.IsCompletedSuccessfully);
            Assert.DoesNotContain(task, tasksManager.Tasks);
            Assert.Contains(task, tasksManager.CompletedTasks);
        }
    }
}
