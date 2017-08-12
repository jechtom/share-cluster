using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ShareCluster.Network
{
    public class HttpApiClient
    {
        static readonly HttpClient appClient = new HttpClient();
        private readonly IMessageSerializer serializer;

        public HttpApiClient(IMessageSerializer serializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Messages.StatusResponse GetStatus(IPEndPoint endpoint)
        {
            return SendRequest<Messages.StatusResponse>(endpoint, nameof(HttpApiController.Status));
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
            var task = appClient.PostAsync(uri, new ByteArrayContent(requestBytes ?? new byte[0]));
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
