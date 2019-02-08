using System;
using System.Linq;
using System.Threading.Tasks;
using ShareCluster.Synchronization;
using Xunit;

namespace ShareCluster.Tests.Synchronization
{
    public class EntityLockTests
    {
        [Fact]
        public void BasicTests()
        {
            var pl = new EntityLock();

            Assert.False(pl.IsMarkedToDelete);
            Assert.True(pl.MarkForDeletionAsync().Wait(300), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void SingelLockRelease()
        {
            var pl = new EntityLock();

            object token = pl.ObtainSharedLock();
            pl.ReleaseSharedLock(token);
            
            Assert.True(pl.MarkForDeletionAsync().Wait(300), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void SingelLockReleaseWait()
        {
            var pl = new EntityLock();

            object token = pl.ObtainSharedLock();
            Task deleteTask = pl.MarkForDeletionAsync();
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.ReleaseSharedLock(token);
            Assert.True(deleteTask.Wait(150), "Waited too long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void DoublelLockReleaseWait()
        {
            var pl = new EntityLock();

            object token1 = pl.ObtainSharedLock(); // lock 1
            object token2 = pl.ObtainSharedLock(); // lock 2
            Task deleteTask = pl.MarkForDeletionAsync(); // mark for delete start
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.ReleaseSharedLock(token1); // unlock 1
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.ReleaseSharedLock(token2); // unlock 2 -> mark for delete completed
            Assert.True(deleteTask.Wait(150), "Waited tool long.");
            Assert.True(pl.IsMarkedToDelete);
        }

        [Fact]
        public void NotAllowLockAfterDelete()
        {
            var pl = new EntityLock();

            object token = pl.ObtainSharedLock(); // lock 1
            Task deleteTask = pl.MarkForDeletionAsync(); // mark for delete start
            Assert.False(pl.TryObtainSharedLock(out var _), "New lock should not be allowed after marked for deletion");
            Assert.False(deleteTask.Wait(150), "Should be locked.");
            pl.ReleaseSharedLock(token); // unlock 1 -> mark for delete completed
            Assert.True(deleteTask.Wait(150), "Waited tool long.");
            Assert.True(pl.IsMarkedToDelete);
        }
    }
}
