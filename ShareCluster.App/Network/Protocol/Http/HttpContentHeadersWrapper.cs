using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace ShareCluster.Network.Protocol.Http
{
    /// <summary>
    /// Wrapper allowing use of <see cref="IHttpHeaderReader"/> and <see cref="IHttpHeaderWriter"/> with <see cref="HttpContentHeaders"/>.
    /// </summary>
    internal class HttpContentHeadersWrapper : IHttpHeaderWriter, IHttpHeaderReader
    {
        private HttpContentHeaders _requestHeaders;
        private HttpResponseHeaders _responseHeaders;

        public HttpContentHeadersWrapper(HttpContentHeaders requestHeaders)
        {
            _requestHeaders = requestHeaders;
        }

        public HttpContentHeadersWrapper(HttpResponseHeaders responseHeaders)
        {
            _responseHeaders = responseHeaders;
        }

        public bool TryReadHeader(string name, out string value)
        {
            if (!_responseHeaders.TryGetValues(name, out IEnumerable<string> values))
            {
                value = null;
                return false;
            }

            var firstValue = values.FirstOrDefault();
            if (firstValue == null)
            {
                value = null;
                return false;
            }

            value = firstValue;
            return true;
        }

        public void WriteHeader(string name, string value) => _requestHeaders.Add(name, value);
    }
}
