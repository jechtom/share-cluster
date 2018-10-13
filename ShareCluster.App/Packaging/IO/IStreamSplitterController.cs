using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Controls how data files are read or written.
    /// </summary>
    public interface IStreamSplitterController : IDisposable
    {
        void OnStreamClosed();
        bool CanWrite { get; }
        bool CanRead { get; }
        IEnumerable<IStreamPart> EnumerateParts();
        long? Length { get; }
        void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart);
    }
}
