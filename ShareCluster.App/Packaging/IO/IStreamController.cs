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

        /// <summary>
        /// This method is called when switching stream parts.
        /// As switching it is considered also starting with first part or finishing with last part.
        /// As switching it is not considered disposing or closing stream before reaching end of stream part.
        /// </summary>
        /// <param name="oldPart">Stream part that is no longer needed or null if first stream part is set.</param>
        /// <param name="newPart">New stream part that will be used on null if last stream part has been finished.</param>
        void OnStreamPartChange(IStreamPart oldPart, IStreamPart newPart);
    }
}
