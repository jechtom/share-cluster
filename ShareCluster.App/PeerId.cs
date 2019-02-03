using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster
{
    public struct PeerId : IEquatable<PeerId>
    {
        public PeerId(Id instanceId, IPEndPoint endPoint)
        {
            if (instanceId.IsNullOrEmpty) throw new ArgumentException("Can't be empty", nameof(instanceId));

            InstanceId = instanceId;
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        }

        public Id InstanceId { get; }
        public IPEndPoint EndPoint { get; }

        public override bool Equals(object obj)
        {
            return ((PeerId)obj).Equals(this);
        }

        public void Validate()
        {
            if (EndPoint == null) throw new InvalidOperationException($"{nameof(PeerId)} validation: Endpoint is null");
            if (EndPoint.Port == 0) throw new InvalidOperationException($"{nameof(PeerId)} validation: Port is invalid");
            if (InstanceId.IsNullOrEmpty) throw new InvalidOperationException($"{nameof(PeerId)} validation: Instance Id is null or empty");
        }

        public bool Equals(PeerId other)
        {
            if (InstanceId != other.InstanceId) return false;
            if (!EndPoint.Equals(other.EndPoint)) return false;
            return true;
        }

        public override int GetHashCode()
        {
            // get value of first max 4 bytes
            int result = InstanceId.GetHashCode();
            result ^= EndPoint.GetHashCode();
            return result;
        }

        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);

        public override string ToString() => $"{EndPoint} with Id={InstanceId:s}";
    }
}
