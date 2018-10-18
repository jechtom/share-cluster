using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DataResponseFault : IMessage
    {
        [ProtoMember(1)]
        public virtual bool IsChoked { get; set; }

        [ProtoMember(2)]
        public virtual bool PackageNotFound { get; set; }

        [ProtoMember(3)]
        public virtual bool PackageSegmentsNotFound { get; set; }

        public static DataResponseFault CreateDataPackageNotFoundMessage() => new DataResponseFault() { PackageNotFound = true };
        public static DataResponseFault CreateDataPackageSegmentsNotFoundMessage() => new DataResponseFault() { PackageSegmentsNotFound = true };
        public static DataResponseFault CreateChokeMessage() => new DataResponseFault() { IsChoked = true };
    }
}
