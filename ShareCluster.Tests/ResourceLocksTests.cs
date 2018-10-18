using ShareCluster.Packaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ShareCluster.Tests.Packaging
{
    public class ResourceLocksTests
    {
        [Fact]
        public void BasicTests()
        {
            var pl = new ResourceLocks();

            Assert.False(pl.IsMarkedToDelete);
            Assert.True(pl.MarkForDelete().Wait(300), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void SingelLockRelease()
        {
            var pl = new ResourceLocks();

            object token = pl.Lock();
            pl.Unlock(token);
            
            Assert.True(pl.MarkForDelete().Wait(300), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void SingelLockReleaseWait()
        {
            var pl = new ResourceLocks();

            object token = pl.Lock();
            Task deleteTask = pl.MarkForDelete();
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.Unlock(token);
            Assert.True(deleteTask.Wait(150), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void DoublelLockReleaseWait()
        {
            var pl = new ResourceLocks();

            object token1 = pl.Lock(); // lock 1
            object token2 = pl.Lock(); // lock 2
            Task deleteTask = pl.MarkForDelete(); // mark for delete start
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.Unlock(token1); // unlock 1
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.Unlock(token2); // unlock 2 -> mark for delete completed
            Assert.True(deleteTask.Wait(150), "Waited tool long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void NotAllowLockAfterDelete()
        {
            var pl = new ResourceLocks();

            object token = pl.Lock(); // lock 1
            Task deleteTask = pl.MarkForDelete(); // mark for delete start
            Assert.False(pl.TryLock(out var _), "New lock should not be allowed after marked for deletion");
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.Unlock(token); // unlock 1 -> mark for delete completed
            Assert.True(deleteTask.Wait(150), "Waited tool long.");
            Assert.True(pl.IsMarkedToDelete);
        }
    }
}
