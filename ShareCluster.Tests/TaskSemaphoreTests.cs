using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.Dto;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ShareCluster.Tests
{
    public class TaskSemaphoreTests
    {
        [Fact]
        public void TestSingleTask()
        {
            var q = new TaskSemaphoreQueue(runningTasksLimit: 1);
            var t1_result = new ManualResetEventSlim();
            q.EnqueueTaskFactory("a", (s) => Task.Run(() => {
                t1_result.Set();
            }));

            Assert.True(t1_result.Wait(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void TestFailShouldNotAffectOthers()
        {
            var q = new TaskSemaphoreQueue(runningTasksLimit: 1);
            var t2_result = new ManualResetEventSlim();
            var e = new ManualResetEventSlim();

            // failed task should not break processing of next tasks

            q.EnqueueTaskFactory("aaaa", (s) => Task.Run(() =>
            {
                throw new InvalidOperationException();
            }));

            q.EnqueueTaskFactory("aaaa", (s) => Task.Run(() =>
            {
                t2_result.Set();
            }));

            Assert.True(t2_result.Wait(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void TestCleaning()
        {
            var q = new TaskSemaphoreQueue(runningTasksLimit: 1);
            var t1_block = new ManualResetEventSlim();
            var t2_result = new ManualResetEventSlim();
            var t3_result = new ManualResetEventSlim();
            var e = new ManualResetEventSlim();

            q.EnqueueTaskFactory("aaaa", (s) => Task.Run(() => {
                t1_block.Wait();
            }));

            q.EnqueueTaskFactory("aaaa", (s) => Task.Run(() => {
                t2_result.Set();
            }));

            q.ClearQueued(); // clean queue (should remove second task)
            t1_block.Set(); // unblock first

            // continue adding third
            q.EnqueueTaskFactory("aaaa", (s) => Task.Run(() => {
                t3_result.Set();
            }));

            // third task should be completed
            Assert.True(t3_result.Wait(TimeSpan.FromSeconds(1)));

            // but second task should not be processed as it was removed from queue before processing
            Assert.False(t2_result.IsSet);
        }

        [Fact]
        public void TestMultipleParallel()
        {
            var q = new TaskSemaphoreQueue(runningTasksLimit: 2);
            
            var t1_block_completion = new ManualResetEventSlim();
            var t2_block_completion = new ManualResetEventSlim();
            var t3_block_completion = new ManualResetEventSlim();
            var t4_block_completion = new ManualResetEventSlim();

            var t1_started = new ManualResetEventSlim();
            var t2_started = new ManualResetEventSlim();
            var t3_started = new ManualResetEventSlim();
            var t4_started = new ManualResetEventSlim();
            var t5_started = new ManualResetEventSlim();

            q.EnqueueTaskFactory("a", (s) => {
                t1_started.Set();
                return Task.Run(() => { t1_block_completion.Wait(); });
            });

            q.EnqueueTaskFactory("a", (s) => {
                t2_started.Set();
                return Task.Run(() => { t2_block_completion.Wait(); });
            });

            q.EnqueueTaskFactory("a", (s) => {
                t3_started.Set();
                return Task.Run(() => { t3_block_completion.Wait(); });
            });

            q.EnqueueTaskFactory("a", (s) => {
                t4_started.Set();
                return Task.Run(() => { t4_block_completion.Wait(); });
            });

            q.EnqueueTaskFactory("a", (s) => {
                t5_started.Set();
                return Task.Run(() => { t4_block_completion.Wait(); });
            });

            // completed tasks: none; running tasks: 1,2; waiting: 3,4,5

            Assert.True(t1_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t2_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(t3_started.IsSet);
            Assert.False(t4_started.IsSet);
            Assert.False(t5_started.IsSet);

            t2_block_completion.Set();
            // completed tasks: 2; running tasks: 1,3; waiting: 4,5

            Assert.True(t1_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t2_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t3_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(t4_started.IsSet);
            Assert.False(t5_started.IsSet);

            t3_block_completion.Set();
            // completed tasks: 2,3; running tasks: 1,4; waiting: 5

            Assert.True(t1_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t2_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t3_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t4_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(t5_started.IsSet);

            t1_block_completion.Set();
            // completed tasks: 2,3,1; running tasks: 4,5; waiting: none

            Assert.True(t1_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t2_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t3_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t4_started.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(t5_started.Wait(TimeSpan.FromSeconds(1)));
        }
    }
}
