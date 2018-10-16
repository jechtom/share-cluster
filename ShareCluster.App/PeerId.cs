using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster
{
    public struct PeerId : IEquatable<PeerId>
    {
        public PeerId(PackageId instanceId, IPEndPoint endpoint)
        {
            if (instanceId.IsNullOrEmpty) throw new ArgumentException("Can't be empty", nameof(instanceId));

            InstanceId = instanceId;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public PackageId InstanceId { get; }
        public IPEndPoint Endpoint { get; }

        public override bool Equals(object obj)
        {
            return ((PeerId)obj).Equals(this);
        }

        public bool Equals(PeerId other)
        {
            if (InstanceId != other.InstanceId) return false;
            if (!Endpoint.Equals(other.Endpoint)) return false;
            return true;
        }

        public override int GetHashCode()
        {
            // get value of first max 4 bytes
            int result = InstanceId.GetHashCode();
            result ^= Endpoint.GetHashCode();
            return result;
        }

        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);

    }
}
