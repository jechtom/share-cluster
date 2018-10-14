using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    /// <summary>
    /// Controls the flow of data in <see cref="ControlledStream"/>.
    /// </summary>
    public interface IStreamController : IDisposable
    {
        void OnStreamClosed();
        bool CanWrite { get; }
        bool CanRead { get; }
        IEnumerable<IStreamPart> EnumerateParts();
        long? Length { get; }
        void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart);
    }
}
