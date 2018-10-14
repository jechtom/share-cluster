using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Tests.IO
{
    public class TestStreamController : IStreamController
    {
        public bool CanWrite { get; set; }

        public bool CanRead { get; set; }

        public long? Length { get; set; }

        public void Dispose()
        {
            EnsureNotDisposed();
            Test_IsDisposed = true;
        }

        public IEnumerable<IStreamPart> EnumerateParts() => Test_StreamPartsSource;
        public void OnStreamClosed()
        {
            EnsureNotDisposed();
            EnsureNotClosed();
            Test_IsStreamClosed = true;
        }

        public void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart)
        {
            EnsureNotDisposed();
            if (oldPart == newPart) throw new InvalidOperationException("Diff old part reported same as new part reported.");
            if (oldPart != Test_CurrentPart) throw new InvalidOperationException("Diff old part reported and stored in test object.");
            if (newPart != null) Test_PartsAll.Add(newPart);
            Test_CurrentPart = newPart;
            Test_OnStreamPartChange?.Invoke(new TestControllerChangeItem(oldPart, newPart, Test_PartsAll.Count - 1));
        }

        private void EnsureNotDisposed()
        {
            if (Test_IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        private void EnsureNotClosed()
        {
            if (Test_IsStreamClosed) throw new InvalidOperationException("Stream already marked as closed.");
        }

        public bool Test_IsDisposed { get; set; } = false;
        public bool Test_IsStreamClosed { get; set; } = false;
        public List<IStreamPart> Test_PartsAll { get; set; } = new List<IStreamPart>();

        
        public IStreamPart Test_CurrentPart { get; set; } = null;
        public IEnumerable<IStreamPart> Test_StreamPartsSource { get; set; } = Enumerable.Empty<IStreamPart>();
        public Action<TestControllerChangeItem> Test_OnStreamPartChange { get; set; } = null;

        public TestStreamController Test_Writable()
        {
            CanWrite = true;
            return this;
        }

        public TestStreamController Test_Readable()
        {
            CanRead = true;
            return this;
        }


        public TestStreamController Test_OnChange(Action<TestControllerChangeItem> action)
        {
            Test_OnStreamPartChange = action;
            return this;
        }

        public TestStreamController Test_WithParts(params IStreamPart[] parts)
        {
            Test_StreamPartsSource = parts;
            return this;
        }
    }
}
