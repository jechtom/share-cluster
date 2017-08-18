using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ShareCluster.Network
{
    public class HttpApiClient
    {
        static readonly HttpClient appClient = new HttpClient();
        private readonly IMessageSerializer serializer;
        private readonly CompatibilityChecker compatibility;
        private readonly InstanceHash instanceHash;

        public HttpApiClient(IMessageSerializer serializer, CompatibilityChecker compatibility, InstanceHash instanceHash)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
            this.instanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
        }

        public Messages.DiscoveryMessage GetStatus(IPEndPoint endpoint, Messages.DiscoveryMessage message)
        {
            return SendRequest<Messages.DiscoveryMessage, Messages.DiscoveryMessage>(endpoint, nameof(HttpApiController.Discovery), message);
        }

        public Messages.PackageResponse GetPackage(IPEndPoint endpoint, Messages.PackageRequest message)
        {
            return SendRequest<Messages.PackageRequest, Messages.PackageResponse>(endpoint, nameof(HttpApiController.Package), message);
        }

        private TRes SendRequest<TRes>(IPEndPoint endpoint,string apiName)
        {
            return SendRequest<object, TRes>(endpoint, apiName, null, sendRequest: false, receiveResponse: true);
        }

        private TRes SendRequest<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req)
        {
            return SendRequest<object, TRes>(endpoint, apiName, req, sendRequest: true, receiveResponse: true);
        }

        private TRes SendRequest<TReq, TRes>(IPEndPoint endpoint, string apiName, TReq req, bool sendRequest, bool receiveResponse)
        {
            string uri = $"http://{endpoint}/api/{apiName}";
            byte[] requestBytes = null;
            if(sendRequest)
            {
                requestBytes = serializer.Serialize(req);
            }

            var requestContent = new ByteArrayContent(requestBytes ?? new byte[0]);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(serializer.MimeType);
            requestContent.Headers.Add(HttpRequestHeaderValidator.VersionHeaderName, compatibility.Version.ToString());
            requestContent.Headers.Add(HttpRequestHeaderValidator.InstanceHeaderName, instanceHash.Hash.ToString());

            var task = appClient.PostAsync(uri, requestContent);
            var resultMessage = task.Result;
            resultMessage.EnsureSuccessStatusCode();

            if(receiveResponse)
            {
                var stream = task.Result.Content.ReadAsStreamAsync().Result;
                return serializer.Deserialize<TRes>(stream);
            }
            return default(TRes);
        }
    }
}
