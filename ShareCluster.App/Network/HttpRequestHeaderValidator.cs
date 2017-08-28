using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Network
{
    public class HttpRequestHeaderValidator : IActionFilter
    {

        public const string VersionHeaderName = "X-ShareClusterVersion";
        public const string InstanceHeaderName = "X-ShareClusterInstance";
        private readonly ILogger<HttpRequestHeaderValidator> logger;

        public HttpRequestHeaderValidator(ILogger<HttpRequestHeaderValidator> logger, CompatibilityChecker compatibilityChecker, InstanceHash instanceHash)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CompatibilityChecker = compatibilityChecker ?? throw new System.ArgumentNullException(nameof(compatibilityChecker));
            InstanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
        }

        public CompatibilityChecker CompatibilityChecker { get; }
        public InstanceHash InstanceHash { get; }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // nothing to do
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if(!context.HttpContext.Request.Headers.TryGetValue(VersionHeaderName, out StringValues valueString))
            {
                ProcessInvalidVersion(context, $"Missing header {VersionHeaderName}");
                return;
            }

            if(!ClientVersion.TryParse(valueString, out ClientVersion version))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {VersionHeaderName}");
                return;
            }

            if(!CompatibilityChecker.IsCompatibleWith(context.HttpContext.Connection.RemoteIpAddress.ToString(), version))
            {
                ProcessInvalidVersion(context, $"Server is incompatible with version defined in header {VersionHeaderName}");
                return;
            }

            // validate if this is not request from myself
            if (!context.HttpContext.Request.Headers.TryGetValue(InstanceHeaderName, out valueString))
            {
                ProcessInvalidVersion(context, $"Missing header {InstanceHeaderName}");
                return;
            }

            if (!Hash.TryParse(valueString, out Hash hash))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {InstanceHeaderName}");
                return;
            }


            var controller = (IHttpApiController)context.Controller;
            controller.RemoteIpAddress = context.HttpContext.Connection.RemoteIpAddress;
            controller.PeerId = hash;
            controller.IsLoopback = hash.Equals(InstanceHash.Hash);
        }

        private void ProcessInvalidVersion(ActionExecutingContext context, string message)
        {
            logger.LogTrace($"{context.HttpContext.Connection.RemoteIpAddress}: {message}");
            context.Result = new ContentResult()
            {
                Content = message,
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
        
    }
}