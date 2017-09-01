using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ShareCluster.Network.Http
{
    public class HttpFilterOnlyLocal : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var connection = context.HttpContext.Connection;
            bool isLocal = connection.RemoteIpAddress != null && IPAddress.IsLoopback(connection.RemoteIpAddress);

            if (!isLocal)
            {
                context.Result = new NotFoundResult();
            }
        }
    }
}
