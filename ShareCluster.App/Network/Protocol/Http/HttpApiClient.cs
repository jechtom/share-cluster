using ShareCluster.Network.Protocol.Messages;
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

namespace ShareCluster.Network.Protocol.Http
{
    public class HttpApiClient : IApiClient
    {
        private readonly HttpClient _appClient = new HttpClient();
        private readonly IMessageSerializer _serializer;
        private readonly HttpCommonHeadersProcessor _headersProcessor;

        //enable to use Fiddler @ localhost: 
        //string BuildUrl(IPEndPoint endPoint, string apiName) => $"http://localhost.fiddler:{endPoint.Port}/api/{apiName}";

        string BuildUrl(IPEndPoint endPoint, string apiPath) => $"http://{endPoint}{apiPath}";

        public HttpApiClient(IMessageSerializer serializer, HttpCommonHeadersProcessor headersProcessor)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _headersProcessor = headersProcessor ?? throw new ArgumentNullException(nameof(headersProcessor));
        }

        public PackageResponse GetPackage(IPEndPoint endpoint, PackageRequest message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return SendRequestAndGetResponse<PackageRequest, PackageResponse>(endpoint, HttpApiConstants.UrlGetPackage, message);
        }

        public async Task<CatalogDataResponse> GetCatalogAsync(IPEndPoint endpoint, CatalogDataRequest message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return await SendRequestAndGetResponeAsync<CatalogDataRequest, CatalogDataResponse>(endpoint, HttpApiConstants.UrlGetCatalog, message);
        }

        public async Task<DataResponseFault> GetDataStreamAsync(IPEndPoint endpoint, DataRequest message, ProcessStreamAsyncDelegate processStreamDelegate)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (processStreamDelegate == null)
            {
                throw new ArgumentNullException(nameof(processStreamDelegate));
            }

            (HttpResponseMessage resultMessage, CommonHeaderData resultHeaders)
                = await SendRequestAsync(endpoint, HttpApiConstants.UrlGetData, message, stream: true);

            using (resultMessage)
            {
                using (Stream stream = await resultMessage.Content.ReadAsStreamAsync())
                {
                    // unexpected response (expected stream) == fault
                    if (!resultHeaders.TypeIsStream)
                    {
                        return _serializer.Deserialize<DataResponseFault>(stream);
                    }

                    // read which segments are returned in stream
                    var segments = _headersProcessor.ReadAndValidateSegmentsHeader(endpoint.Address, new HttpContentHeadersWrapper(resultMessage.Headers));

                    // proccess
                    await processStreamDelegate.Invoke(segments, stream);
                }
                return null; // success
            }
        }

        private async Task<(HttpResponseMessage, CommonHeaderData)> SendRequestAsync<TReq>(IPEndPoint endpoint, string apiName, TReq req, bool stream = false)
        {
            var uri = BuildUrl(endpoint, apiName);
            var requestBytes = _serializer.Serialize(req);

            if (requestBytes == null)
            {
                throw new InvalidOperationException("Message byte array can't be null.");
            }

            var requestContent = new ByteArrayContent(requestBytes);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(_serializer.MimeType);
            _headersProcessor.AddCommonHeaders(new HttpContentHeadersWrapper(requestContent.Headers), typeof(TReq).Name);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Content = requestContent;
                HttpResponseMessage resultMessage = null;
                CommonHeaderData resultHeaders = null;
                try
                {
                    resultMessage = await _appClient.SendAsync(request, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
                    resultMessage.EnsureSuccessStatusCode();
                    resultHeaders = _headersProcessor.ReadAndValidateAndProcessCommonHeaders(
                        endpoint.Address,
                        PeerCommunicationDirection.TcpOutgoing,
                        new HttpContentHeadersWrapper(resultMessage.Headers)
                    );
                }
                catch
                {
                    if (resultMessage != null)
                    {
                        resultMessage.Dispose();
                    }
                    throw;
                }
                return (resultMessage, resultHeaders);
            }
        }

        private TRes SendRequestAndGetResponse<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            return SendRequestAndGetResponeAsync<TReq, TRes>(endpoint, apiName, req).Result;
        }

        private async Task<TRes> SendRequestAndGetResponeAsync<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            (HttpResponseMessage resultMessage, CommonHeaderData resultHeaders)
                = await SendRequestAsync(endpoint, apiName, req);

            using (resultMessage)
            {
                using (Stream stream = await resultMessage.Content.ReadAsStreamAsync())
                {
                    return _serializer.Deserialize<TRes>(stream);
                }
            }
        }
    }
}
