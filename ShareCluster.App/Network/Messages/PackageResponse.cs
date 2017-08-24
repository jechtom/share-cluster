﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Messages
{
    [ProtoContract]
    public class PackageResponse : IMessage
    {
        [ProtoMember(1)]
        public Packaging.Dto.PackageMeta Meta { get; set; }
    }
}
