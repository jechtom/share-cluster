using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class DataResponseFaul : IMessage
    {
        [ProtoMember(1)]
        public virtual bool IsChoked { get; set; }

        [ProtoMember(2)]
        public virtual bool PackageNotFound { get; set; }

        [ProtoMember(3)]
        public virtual bool PackageSegmentsNotFound { get; set; }

        public static DataResponseFaul CreateDataPackageNotFoundMessage() => new DataResponseFaul() { PackageNotFound = true };
        public static DataResponseFaul CreateDataPackageSegmentsNotFoundMessage() => new DataResponseFaul() { PackageSegmentsNotFound = true };
        public static DataResponseFaul CreateChokeMessage() => new DataResponseFaul() { IsChoked = true };
    }
}
