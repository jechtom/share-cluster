using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    public class NetworkThrottling
    {
        public NetworkThrottling(NetworkSettings networkSettings)
        {
            if (networkSettings == null)
            {
                throw new ArgumentNullException(nameof(networkSettings));
            }

            UploadSlots = new Item(networkSettings.MaximumUploadsSlots);
            DownloadSlots = new Item(networkSettings.MaximumDownloadSlots);
        }

        public Item UploadSlots { get; }
        public Item DownloadSlots { get; }

        public class Item
        {
            private readonly int _limit;
            private int _count;

            public Item(int limit)
            {
                _limit = limit;
            }

            public bool TryUseSlot()
            {
                int newCount = Interlocked.Increment(ref _count);
                if (newCount > _limit)
                {
                    Interlocked.Decrement(ref _count);
                    return false;
                }
                return true;
            }

            public void ReleaseSlot()
            {
                int newCount = Interlocked.Decrement(ref _count);
                if (newCount < 0)
                {
                    throw new InvalidOperationException("Invalid release.");
                }
            }

            public int Limit => _limit;

            // for short period of time when trying to allocate new slot if there are no more
            // the value will be greater than limit for short time - better limit then result
            public int Used => Math.Min(Limit, _count);
        }
    }
}
