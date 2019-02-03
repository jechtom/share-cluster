using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShareCluster.Network.Messages;

namespace ShareCluster.Network.Udp
{
    public class UdpPeerDiscoverySerializer
    {
        private readonly IMessageSerializer _messageSerializer;

        /// <summary>
        /// Gets current version of serializer. This is mechanism to prevent version mismatch if newer version of serializer will be released.
        /// </summary>
        public VersionNumber SerializerVersion { get; } = new VersionNumber(1);

        public UdpPeerDiscoverySerializer(IMessageSerializer messageSerializer)
        {
            _messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
        }

        public byte[] Serialize(DiscoveryAnnounceMessage message)
        {
            ValidateMessage(message);
            using (var memStream = new MemoryStream())
            {
                _messageSerializer.Serialize(SerializerVersion, memStream);
                _messageSerializer.Serialize(message, memStream);
                return memStream.ToArray();
            }
        }

        public DiscoveryAnnounceMessage Deserialize(Stream memStream)
        {
            // deserialize network protocol version and ignore if incompatible
            VersionNumber messageVersion = _messageSerializer.Deserialize<VersionNumber>(memStream);
            if (messageVersion != SerializerVersion)
            {
                return null;
            }

            // deserialize following message
            DiscoveryAnnounceMessage announceMessage = _messageSerializer.Deserialize<DiscoveryAnnounceMessage>(memStream);

            ValidateMessage(announceMessage);

            return announceMessage;
        }

        private void ValidateMessage(DiscoveryAnnounceMessage announceMessage)
        {
            if (announceMessage == null)
            {
                throw new ArgumentNullException(nameof(announceMessage));
            }

            if (announceMessage.PeerId.IsNullOrEmpty)
            {
                throw new InvalidOperationException($"Id is null or empty.");
            }

            if (announceMessage.ServicePort == 0)
            {
                throw new InvalidOperationException("Invalid port 0");
            }
        }

    }
}
