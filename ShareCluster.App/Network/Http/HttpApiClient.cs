using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    public class HttpApiClient
    {
        private readonly HttpClient _appClient = new HttpClient();
        private readonly IMessageSerializer _serializer;
        private readonly CompatibilityChecker _compatibility;
        private readonly InstanceId _instanceHash;
        private readonly NetworkSettings _networkSettings;
        private readonly ILocalPackageRegistry _localPackageRegistry;

        //enable to use Fiddler @ localhost: 
        //string BuildUrl(IPEndPoint endPoint, string apiName) => $"http://localhost.fiddler:{endPoint.Port}/api/{apiName}";

        string BuildUrl(IPEndPoint endPoint, string apiName) => $"http://{endPoint}/api/{apiName}";

        public HttpApiClient(IMessageSerializer serializer, CompatibilityChecker compatibility, InstanceId instanceHash, NetworkSettings networkSettings, ILocalPackageRegistry localPackageRegistry)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            _instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _localPackageRegistry = localPackageRegistry ?? throw new ArgumentNullException(nameof(localPackageRegistry));
        }

        public Task<Messages.PackageStatusResponse> GetPackageStatusAsync(IPEndPoint endpoint, Messages.PackageStatusRequest message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return SendRequestAndGetResponeAsync<Messages.PackageStatusRequest, Messages.PackageStatusResponse> (endpoint, nameof(HttpApiController.GetPackageStatus), message);
        }

        public Messages.PackageResponse GetPackage(IPEndPoint endpoint, Messages.PackageRequest message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return SendRequestAndGetResponse<Messages.PackageRequest, Messages.PackageResponse>(endpoint, nameof(HttpApiController.GetPackage), message);
        }

        public async Task<Messages.CatalogDataResponse> GetCatalogAsync(IPEndPoint endpoint, Messages.CatalogDataRequest message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return await SendRequestAndGetResponeAsync<Messages.CatalogDataRequest, Messages.CatalogDataResponse>(endpoint, nameof(HttpApiController.GetCatalog), message);
        }

        public async Task<Messages.DataResponseFault> DownloadPartsAsync(IPEndPoint endpoint, Messages.DataRequest message, Lazy<Stream> streamToWriteLazy)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (HttpResponseMessage resultMessage = await SendRequestAsync(endpoint, nameof(HttpApiController.Data), message, stream: true))
            {
                using (Stream stream = await resultMessage.Content.ReadAsStreamAsync())
                {
                    // unexpected response (expected stream) == fault 
                    if(resultMessage.Headers.TryGetValues(HttpRequestHeaderValidator.TypeHeaderName, out IEnumerable<string> typeHeaderValues))
                    {
                        return _serializer.Deserialize<Messages.DataResponseFault>(stream);
                    }

                    // write to target stream
                    Stream streamToWrite = streamToWriteLazy.Value;
                    await stream.CopyToAsync(streamToWrite);
                }
                return null; // success
            }
        }
        
        private async Task<HttpResponseMessage> SendRequestAsync<TReq>(IPEndPoint endpoint, string apiName, TReq req, bool stream = false)
        {
            string uri = BuildUrl(endpoint, apiName);
            byte[] requestBytes = _serializer.Serialize(req);

            if(requestBytes == null)
            {
                throw new InvalidOperationException("Message byte array can't be null.");
            }

            var requestContent = new ByteArrayContent(requestBytes);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(_serializer.MimeType);
            requestContent.Headers.Add(HttpRequestHeaderValidator.VersionHeaderName, _compatibility.NetworkProtocolVersion.ToString());
            requestContent.Headers.Add(HttpRequestHeaderValidator.InstanceHeaderName, _instanceHash.Value.ToString());
            requestContent.Headers.Add(HttpRequestHeaderValidator.TypeHeaderName, typeof(TReq).Name);
            requestContent.Headers.Add(HttpRequestHeaderValidator.ServicePortHeaderName, _networkSettings.TcpServicePort.ToString());
            requestContent.Headers.Add(HttpRequestHeaderValidator.CatalogVersionHeaderName, _localPackageRegistry.Version.ToString());
            
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Content = requestContent;
                HttpResponseMessage resultMessage = null;
                try
                {
                    resultMessage = await _appClient.SendAsync(request, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
                    resultMessage.EnsureSuccessStatusCode();
                }
                catch
                {
                    if (resultMessage != null)
                    {
                        resultMessage.Dispose();
                    }
                    throw;
                }
                return resultMessage;
            }
        }

        private TRes SendRequestAndGetResponse<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            return SendRequestAndGetResponeAsync<TReq, TRes>(endpoint, apiName, req).Result;
        }

        private async Task<TRes> SendRequestAndGetResponeAsync<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            using (HttpResponseMessage resultMessage = await SendRequestAsync(endpoint, apiName, req))
            {
                using (Stream stream = await resultMessage.Content.ReadAsStreamAsync())
                {
                    return _serializer.Deserialize<TRes>(stream);
                }
            }
        }
    }
}
