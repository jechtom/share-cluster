using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Protocol.Messages
{
    [ProtoContract]
    public class DataResponseFault : IMessage
    {
        /// <summary>
        /// If set, it indicates that all upload slots of this peer are used.
        /// </summary>
        [ProtoMember(1)]
        public virtual bool IsChoked { get; set; }

        /// <summary>
        /// If set, it indicates that package does not exists on peer.
        /// Probably has been deleted but catalog change is not propagated yet.
        /// </summary>
        [ProtoMember(2)]
        public virtual bool PackageNotFound { get; set; }

        /// <summary>
        /// If set, it indicates that peer don't have any useful segments.
        /// </summary>
        [ProtoMember(3)]
        public virtual bool PackageSegmentsNoMatch { get; set; }

        public static DataResponseFault CreateDataPackageNotFoundMessage() =>
            new DataResponseFault() { PackageNotFound = true };

        public static DataResponseFault CreateDataPackageSegmentsNoMatchMessage() =>
            new DataResponseFault() { PackageSegmentsNoMatch = true };

        public static DataResponseFault CreateChokeMessage() =>
            new DataResponseFault() { IsChoked = true };
    }
}
