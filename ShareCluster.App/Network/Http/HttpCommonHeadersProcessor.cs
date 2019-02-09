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

        public CommonHeaderData ReadAndValidateAndProcessCommonHeaders(IPAddress remoteAddress, PeerCommunicationType peerCommunicationType, IHttpHeaderReader headerReader)
        {
            // validate version
            if (!headerReader.TryReadHeader(VersionHeaderName, out string valueStringInstance))
            {
                Fail(remoteAddress, $"Missing header {VersionHeaderName}");
            }

            if (!VersionNumber.TryParse(valueStringInstance, out VersionNumber version))
            {
                Fail(remoteAddress, $"Invalid value of header {VersionHeaderName}");
            }

            if (!_compatibility.IsCompatibleWith(remoteAddress, version))
            {
                Fail(remoteAddress, $"Server is incompatible with version defined in header {VersionHeaderName}");
            }

            // validate instance type header
            if (!headerReader.TryReadHeader(InstanceHeaderName, out valueStringInstance))
            {
                Fail(remoteAddress, $"Missing header {InstanceHeaderName}");
            }

            if (!Id.TryParse(valueStringInstance, out Id instanceId))
            {
                Fail(remoteAddress, $"Invalid value of header {InstanceHeaderName}");
            }

            // validate input type header
            if (!headerReader.TryReadHeader(TypeHeaderName, out string typeString))
            {
                Fail(remoteAddress, $"Missing header {TypeHeaderName}");
            }

            // validate service port header
            if (!headerReader.TryReadHeader(ServicePortHeaderName, out string valueStringPort))
            {
                Fail(remoteAddress, $"Missing header {ServicePortHeaderName}");
            }

            if (!ushort.TryParse(valueStringPort, out ushort servicePort))
            {
                Fail(remoteAddress, $"Invalid value of header {ServicePortHeaderName}");
            }

            // validate catalog version header
            if (!headerReader.TryReadHeader(CatalogVersionHeaderName, out string valueStringCatalog))
            {
                Fail(remoteAddress, $"Missing header {CatalogVersionHeaderName}");
            }

            if (!VersionNumber.TryParse(valueStringCatalog, out VersionNumber catalogVersion))
            {
                Fail(remoteAddress, $"Invalid value of header {CatalogVersionHeaderName}");
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

        private void Fail(IPAddress remoteAddress, string message)
        {
            _logger.LogTrace($"{remoteAddress}: {message}");
        }
    }
}
