using System;

namespace ShareCluster.Network
{
    public class PackageDownloadSlotException : Exception
    {
        public PackageDownloadSlotException(Exception innerException) : base("Unexpected download failure.", innerException)
        {
            Fault = PackageDownloadSlotFault.Exception;
        }

        public PackageDownloadSlotException(PackageDownloadSlotFault fault) : base(fault.ToString())
        {
            Fault = fault;
        }

        public PackageDownloadSlotFault Fault { get; }
    }
}
