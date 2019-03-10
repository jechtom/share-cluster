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

        [ProtoMember(4)]
        public virtual byte[] PackageSegmentsKnown { get; set; }

        public static DataResponseFault CreateDataPackageNotFoundMessage() => new DataResponseFault() { PackageNotFound = true };

        public static DataResponseFault CreateDataPackageSegmentsNotFoundMessage(byte[] packageSegmentsKnown) =>
            new DataResponseFault() {
                PackageSegmentsNotFound = true,
                PackageSegmentsKnown = packageSegmentsKnown ?? throw new ArgumentNullException(nameof(packageSegmentsKnown))
            };

        public static DataResponseFault CreateChokeMessage() => new DataResponseFault() { IsChoked = true };
    }
}
