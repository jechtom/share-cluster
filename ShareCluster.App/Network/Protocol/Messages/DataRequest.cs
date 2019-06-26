using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Protocol.Messages
{
    [ProtoContract]
    public class DataRequest : IMessage
    {
        [ProtoMember(1)]
        public virtual Id PackageId { get; set; }

        /// <summary>
        /// Gets or sets bitmap of segments we already have.
        /// This is used by peer to choose which segments it should send in response.
        /// </summary>
        [ProtoMember(2)]
        public virtual byte[] DownloadedSegmentsBitmap { get; set; }
    }
}
