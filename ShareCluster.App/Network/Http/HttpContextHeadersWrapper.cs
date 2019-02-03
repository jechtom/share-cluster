using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Wrapper allowing use of <see cref="IHttpHeaderReader"/> and <see cref="IHttpHeaderWriter"/> with <see cref="HttpContext"/>.
    /// </summary>
    public class HttpContextHeadersWrapper : IHttpHeaderWriter, IHttpHeaderReader
    {
        private readonly HttpContext _httpContext;

        public HttpContextHeadersWrapper(HttpContext httpContext)
        {
            _httpContext = httpContext;
        }

        public bool TryReadHeader(string name, out string value)
        {
            if (!_httpContext.Request.Headers.TryGetValue(name, out StringValues values))
            {
                value = null;
                return false;
            }

            string firstValue = values;
            if (firstValue == null)
            {
                value = null;
                return false;
            }

            value = firstValue;
            return true;
        }

        public void WriteHeader(string name, string value) => _httpContext.Response.Headers.Add(name, value);
    }
}
