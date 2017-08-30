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

namespace ShareCluster.Network
{
    public class HttpApiClient
    {
        static readonly HttpClient appClient = new HttpClient();
        private readonly IMessageSerializer serializer;
        private readonly CompatibilityChecker compatibility;
        private readonly InstanceHash instanceHash;

        //enable to use Fiddler @ localhost: string BuildUrl(IPEndPoint endPoint, string apiName) => $"http://localhost.fiddler:{endPoint.Port}/api/{apiName}";
        string BuildUrl(IPEndPoint endPoint, string apiName) => $"http://{endPoint}/api/{apiName}";


        public HttpApiClient(IMessageSerializer serializer, CompatibilityChecker compatibility, InstanceHash instanceHash)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            this.instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
        }

        public Task<Messages.PackageStatusResponse> GetPackageStatusAsync(IPEndPoint endpoint, Messages.PackageStatusRequest message)
        {
            return SendRequestAndGetResponeAsync<Messages.PackageStatusRequest, Messages.PackageStatusResponse> (endpoint, nameof(HttpApiController.PackageStatus), message);
        }

        public Messages.StatusUpdateMessage GetStatus(IPEndPoint endpoint, Messages.StatusUpdateMessage message)
        {
            return SendRequestAndGetRespone<Messages.StatusUpdateMessage, Messages.StatusUpdateMessage>(endpoint, nameof(HttpApiController.StatusUpdate), message);
        }

        public Messages.PackageResponse GetPackage(IPEndPoint endpoint, Messages.PackageRequest message)
        {
            return SendRequestAndGetRespone<Messages.PackageRequest, Messages.PackageResponse>(endpoint, nameof(HttpApiController.Package), message);
        }

        public async Task<Messages.DataResponseFaul> DownloadPartsAsync(IPEndPoint endpoint, Messages.DataRequest message, Lazy<Stream> streamToWriteLazy)
        {
            using (HttpResponseMessage resultMessage = await SendRequestAsync(endpoint, nameof(HttpApiController.Data), message, stream: true))
            {
                using (var stream = await resultMessage.Content.ReadAsStreamAsync())
                {
                    // unexpected response (expected stream) == fault 
                    if(resultMessage.Headers.TryGetValues(HttpRequestHeaderValidator.TypeHeaderName, out IEnumerable<string> typeHeaderValues))
                    {
                        return serializer.Deserialize<Messages.DataResponseFaul>(stream);
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
            byte[] requestBytes = serializer.Serialize(req);

            var requestContent = new ByteArrayContent(requestBytes ?? new byte[0]);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(serializer.MimeType);
            requestContent.Headers.Add(HttpRequestHeaderValidator.VersionHeaderName, compatibility.Version.ToString());
            requestContent.Headers.Add(HttpRequestHeaderValidator.InstanceHeaderName, instanceHash.Hash.ToString());

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Content = requestContent;

            var resultMessage = await appClient.SendAsync(request, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
            resultMessage.EnsureSuccessStatusCode();
            return resultMessage;
        }

        private TRes SendRequestAndGetRespone<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            using (HttpResponseMessage resultMessage = SendRequestAsync(endpoint, apiName, req).Result)
            {
                var stream = resultMessage.Content.ReadAsStreamAsync().Result;
                return serializer.Deserialize<TRes>(stream);
            }
        }

        private async Task<TRes> SendRequestAndGetResponeAsync<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            using (var resultMessage = await SendRequestAsync(endpoint, apiName, req))
            {
                var stream = await resultMessage.Content.ReadAsStreamAsync();
                return serializer.Deserialize<TRes>(stream);
            }
        }
    }
}
