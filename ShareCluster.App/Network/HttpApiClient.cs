using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
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

        public Messages.PackageStatusResponse GetPackageStatus(IPEndPoint endpoint, Messages.PackageStatusRequest message)
        {
            return SendRequestAndGetRespone<Messages.PackageStatusRequest, Messages.PackageStatusResponse> (endpoint, nameof(HttpApiController.PackageStatus), message);
        }

        public Messages.StatusUpdateMessage GetStatus(IPEndPoint endpoint, Messages.StatusUpdateMessage message)
        {
            return SendRequestAndGetRespone<Messages.StatusUpdateMessage, Messages.StatusUpdateMessage>(endpoint, nameof(HttpApiController.StatusUpdate), message);
        }

        public Messages.PackageResponse GetPackage(IPEndPoint endpoint, Messages.PackageRequest message)
        {
            return SendRequestAndGetRespone<Messages.PackageRequest, Messages.PackageResponse>(endpoint, nameof(HttpApiController.Package), message);
        }

        public async Task DownloadPartsAsync(IPEndPoint endpoint, Messages.DataRequest message, Stream streamToWrite)
        {
            HttpResponseMessage resultMessage = await SendRequest(endpoint, nameof(HttpApiController.Data), message, stream: true);
            var stream = await resultMessage.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(streamToWrite);
        }
        
        private async Task<HttpResponseMessage> SendRequest<TReq>(IPEndPoint endpoint, string apiName, TReq req, bool stream = false)
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
            var taskSend = SendRequest(endpoint, apiName, req);
            HttpResponseMessage resultMessage = taskSend.Result;
            var stream = resultMessage.Content.ReadAsStreamAsync().Result;
            return serializer.Deserialize<TRes>(stream);
        }
    }
}
