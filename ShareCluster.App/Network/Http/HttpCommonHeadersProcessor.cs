using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Reads and writes common headers.
    /// </summary>
    public class HttpCommonHeadersProcessor
    {
        public const string VersionHeaderName = "X-ShareClusterVersion";
        public const string InstanceHeaderName = "X-ShareClusterInstance";
        public const string TypeHeaderName = "X-ShareClusterType";
        public const string TypeHeaderForStream = "stream";
        public const string ServicePortHeaderName = "X-ShareClusterPort";
        public const string CatalogVersionHeaderName = "X-ShareClusterCatalog";
        private readonly ILogger<HttpCommonHeadersProcessor> _logger;
        private readonly PeerAppVersionCompatibility _compatibility;
        private readonly InstanceId _instanceId;
        private readonly NetworkSettings _networkSettings;
        private readonly ILocalPackageRegistry _localPackageRegistry;

        public HttpCommonHeadersProcessor(ILogger<HttpCommonHeadersProcessor> logger, PeerAppVersionCompatibility compatibility, InstanceId instanceId, NetworkSettings networkSettings, ILocalPackageRegistry localPackageRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
        }

        public void AddCommonHeaders(IHttpHeaderWriter headerWriter, string typeString)
        {
            if (headerWriter == null)
            {
                throw new ArgumentNullException(nameof(headerWriter));
            }

            if (string.IsNullOrEmpty(typeString))
            {
                throw new ArgumentException("message", nameof(typeString));
            }

            headerWriter.WriteHeader(TypeHeaderName, typeString);
            headerWriter.WriteHeader(VersionHeaderName, _compatibility.LocalVersion.ToString());
            headerWriter.WriteHeader(InstanceHeaderName, _instanceId.Value.ToString());
            headerWriter.WriteHeader(ServicePortHeaderName, _networkSettings.TcpServicePort.ToString());
            headerWriter.WriteHeader(CatalogVersionHeaderName, _localPackageRegistry.Version.ToString());
        }

        public CommonHeaderData ReadAndValidateAndProcessCommonHeaders(IPAddress remoteAddress, PeerCommunicationDirection peerCommunicationType, IHttpHeaderReader headerReader)
        {
            // validate version
            if (!headerReader.TryReadHeader(VersionHeaderName, out string valueStringInstance))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {VersionHeaderName}");
            }

            if (!VersionNumber.TryParse(valueStringInstance, out VersionNumber version))
            {
                ThrowHeaderError(remoteAddress, $"Invalid value of header {VersionHeaderName}");
            }

            _compatibility.ThrowIfNotCompatibleWith(remoteAddress, version);

            // validate instance type header
            if (!headerReader.TryReadHeader(InstanceHeaderName, out valueStringInstance))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {InstanceHeaderName}");
            }

            if (!Id.TryParse(valueStringInstance, out Id instanceId))
            {
                ThrowHeaderError(remoteAddress, $"Invalid value of header {InstanceHeaderName}");
            }

            // validate input type header
            if (!headerReader.TryReadHeader(TypeHeaderName, out string typeString))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {TypeHeaderName}");
            }

            // validate service port header
            if (!headerReader.TryReadHeader(ServicePortHeaderName, out string valueStringPort))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {ServicePortHeaderName}");
            }

            if (!ushort.TryParse(valueStringPort, out ushort servicePort))
            {
                ThrowHeaderError(remoteAddress, $"Invalid value of header {ServicePortHeaderName}");
            }

            // validate catalog version header
            if (!headerReader.TryReadHeader(CatalogVersionHeaderName, out string valueStringCatalog))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {CatalogVersionHeaderName}");
            }

            if (!VersionNumber.TryParse(valueStringCatalog, out VersionNumber catalogVersion))
            {
                ThrowHeaderError(remoteAddress, $"Invalid value of header {CatalogVersionHeaderName}");
            }

            // convert ::ffff:192.168.1.1 format to IPv4 (this happened to me on localhost, not sure why)
            if (remoteAddress.IsIPv4MappedToIPv6) remoteAddress = remoteAddress.MapToIPv4();

            var serviceEndpoint = new IPEndPoint(remoteAddress, servicePort);
            var peerId = new PeerId(instanceId, serviceEndpoint);
            bool isLoopback = instanceId == _instanceId.Value;

            var result = new CommonHeaderData(catalogVersion, peerId, isLoopback, typeString, peerCommunicationType);
            ProcessResult(result);
            return result;
        }

        private void ProcessResult(CommonHeaderData headerData)
        {
            // update
            HeaderDataParsed.Invoke(this, headerData);
        }

        public event EventHandler<CommonHeaderData> HeaderDataParsed;

        private void ThrowHeaderError(IPAddress remoteAddress, string message)
        {
            string messageWithAddress = $"{remoteAddress}: {message}";
            _logger.LogTrace(messageWithAddress);
            throw new MissingOrInvalidHeaderException(messageWithAddress);
        }
    }
}
