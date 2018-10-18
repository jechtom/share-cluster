using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ShareCluster.Network.Http
{
    public class HttpRequestHeaderValidator : IActionFilter
    {

        public const string VersionHeaderName = "X-ShareClusterVersion";
        public const string InstanceHeaderName = "X-ShareClusterInstance";
        public const string TypeHeaderName = "X-ShareClusterType";
        public const string ServicePortHeaderName = "X-ShareClusterPort";
        public const string CatalogVersionHeaderName = "X-ShareClusterCatalog";

        private readonly ILogger<HttpRequestHeaderValidator> _logger;

        public HttpRequestHeaderValidator(ILogger<HttpRequestHeaderValidator> logger, CompatibilityChecker compatibilityChecker, InstanceId instanceHash)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CompatibilityChecker = compatibilityChecker ?? throw new System.ArgumentNullException(nameof(compatibilityChecker));
            InstanceHash = instanceHash ?? throw new ArgumentNullException(nameof(instanceHash));
        }

        public CompatibilityChecker CompatibilityChecker { get; }
        public InstanceId InstanceHash { get; }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // nothing to do
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // validate version
            if(!context.HttpContext.Request.Headers.TryGetValue(VersionHeaderName, out StringValues valueStringInstance))
            {
                ProcessInvalidVersion(context, $"Missing header {VersionHeaderName}");
                return;
            }

            if(!VersionNumber.TryParse(valueStringInstance, out VersionNumber version))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {VersionHeaderName}");
                return;
            }

            if(!CompatibilityChecker.IsCompatibleWith(CompatibilitySet.NetworkProtocol, context.HttpContext.Connection.RemoteIpAddress.ToString(), version))
            {
                ProcessInvalidVersion(context, $"Server is incompatible with version defined in header {VersionHeaderName}");
                return;
            }

            // validate instance type header
            if (!context.HttpContext.Request.Headers.TryGetValue(InstanceHeaderName, out valueStringInstance))
            {
                ProcessInvalidVersion(context, $"Missing header {InstanceHeaderName}");
                return;
            }

            if (!Id.TryParse(valueStringInstance, out Id instanceId))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {InstanceHeaderName}");
                return;
            }

            // validate input type header
            if (!context.HttpContext.Request.Headers.TryGetValue(TypeHeaderName, out StringValues _))
            {
                ProcessInvalidVersion(context, $"Missing header {TypeHeaderName}");
                return;
            }

            // validate service port header
            if (!context.HttpContext.Request.Headers.TryGetValue(ServicePortHeaderName, out StringValues valueStringPort))
            {
                ProcessInvalidVersion(context, $"Missing header {ServicePortHeaderName}");
                return;
            }

            if (!ushort.TryParse(valueStringPort, out ushort servicePort))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {ServicePortHeaderName}");
                return;
            }

            // validate catalog version header
            if (!context.HttpContext.Request.Headers.TryGetValue(CatalogVersionHeaderName, out StringValues valueStringCatalog))
            {
                ProcessInvalidVersion(context, $"Missing header {CatalogVersionHeaderName}");
                return;
            }

            if (!VersionNumber.TryParse(valueStringCatalog, out VersionNumber catalogVersion))
            {
                ProcessInvalidVersion(context, $"Invalid value of header {CatalogVersionHeaderName}");
                return;
            }

            var serviceEndpoint = new IPEndPoint(context.HttpContext.Connection.RemoteIpAddress, servicePort);

            var controller = (IHttpApiController)context.Controller;
            controller.PeerId = new PeerId(instanceId, serviceEndpoint);
            controller.IsLoopback = instanceId == InstanceHash.Value;
            controller.PeerCatalogVersion = catalogVersion;
        }

        private void ProcessInvalidVersion(ActionExecutingContext context, string message)
        {
            _logger.LogTrace($"{context.HttpContext.Connection.RemoteIpAddress}: {message}");
            context.Result = new ContentResult()
            {
                Content = message,
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
        
    }
}
