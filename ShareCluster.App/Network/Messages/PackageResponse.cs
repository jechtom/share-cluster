using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using ShareCluster.Packaging.Dto;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageResponse : IMessage
    {
        [ProtoMember(1)]
        public virtual PackageContentDefinitionDto Definition { get; set; }
        
        [ProtoMember(2)]
        public virtual bool Found { get; set; }
    }
}
