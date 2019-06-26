using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShareCluster.Network.Protocol.Messages;
using ShareCluster.Packaging;

namespace ShareCluster.Network.Protocol.Http
{
    /// <summary>
    /// Reads and writes common headers.
    /// </summary>
    public class HttpCommonHeadersProcessor
    {
        /// <summary>
        /// Name of header that identifies version of client.
        /// Usage: Compatibility check.
        /// </summary>
        public const string VersionHeaderName = "X-ShareClusterVersion";

        /// <summary>
        /// Name of header that identifies peer random instance Id.
        /// Usage: Sent with all requests and responses.
        /// </summary>
        public const string InstanceHeaderName = "X-ShareClusterInstance";

        /// <summary>
        /// Name of header that defines data type sent in request/response body.
        /// Usage: In some cases it is not clear as multiple types can be returned to single request - for example either data stream or fault reason.
        /// </summary>
        public const string TypeHeaderName = "X-ShareClusterType";

        /// <summary>
        /// Reserved keyword used as <see cref="TypeHeaderName"/> value to identify that response body contains data stream.
        /// </summary>
        public const string TypeHeaderForStream = "stream";

        /// <summary>
        /// Name of header with HTTP service port.
        /// Usage: In all HTTP requests to notify peer what is our HTTP service port.
        /// </summary>
        public const string ServicePortHeaderName = "X-ShareClusterPort";

        /// <summary>
        /// Name of header with version of peers catalog.
        /// Usage: In all HTTP requests and responses to notify peer about current catalog version.
        /// </summary>
        public const string CatalogVersionHeaderName = "X-ShareClusterCatalog";

        /// <summary>
        /// Name of header with identification of data segments sent in body stream.
        /// Usage: In data request response.
        /// </summary>
        public const string SegmentsHeaderName = "X-ShareClusterSegments";

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

        public void AddSegmentsHeader(IHttpHeaderWriter headerWriter, IEnumerable<int> segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (!segments.Any())
            {
                throw new ArgumentException("Empty collection is not allowed.", nameof(segments));
            }

            var segmentsString = JsonConvert.SerializeObject(segments);
            headerWriter.WriteHeader(SegmentsHeaderName, segmentsString);
        }


        public int[] ReadAndValidateSegmentsHeader(IPAddress remoteAddress, IHttpHeaderReader headerReader)
        {
            // read
            if (!headerReader.TryReadHeader(SegmentsHeaderName, out var valueStringSegments))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {SegmentsHeaderName}");
            }

            // deserialize
            var segments = JsonConvert.DeserializeObject<int[]>(valueStringSegments);
            if (segments.Length == 0)
            {
                ThrowHeaderError(remoteAddress, $"Header {SegmentsHeaderName} contains unsupported empty array.");
            }

            return segments;
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
            if (!headerReader.TryReadHeader(VersionHeaderName, out var valueStringInstance))
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
            if (!headerReader.TryReadHeader(TypeHeaderName, out var typeString))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {TypeHeaderName}");
            }

            // validate service port header
            if (!headerReader.TryReadHeader(ServicePortHeaderName, out var valueStringPort))
            {
                ThrowHeaderError(remoteAddress, $"Missing header {ServicePortHeaderName}");
            }

            if (!ushort.TryParse(valueStringPort, out var servicePort))
            {
                ThrowHeaderError(remoteAddress, $"Invalid value of header {ServicePortHeaderName}");
            }

            // validate catalog version header
            if (!headerReader.TryReadHeader(CatalogVersionHeaderName, out var valueStringCatalog))
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
            var isLoopback = instanceId == _instanceId.Value;

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
            var messageWithAddress = $"{remoteAddress}: {message}";
            _logger.LogTrace(messageWithAddress);
            throw new MissingOrInvalidHeaderException(messageWithAddress);
        }
    }
}
