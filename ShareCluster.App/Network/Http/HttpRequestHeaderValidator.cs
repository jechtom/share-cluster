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
        private readonly ILogger<HttpRequestHeaderValidator> _logger;
        private readonly HttpCommonHeadersProcessor _headersProcessor;
        private readonly PeerController _peerController;

        public HttpRequestHeaderValidator(ILogger<HttpRequestHeaderValidator> logger, HttpCommonHeadersProcessor headersProcessor, PeerController peerController)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _headersProcessor = headersProcessor ?? throw new ArgumentNullException(nameof(headersProcessor));
            _peerController = peerController ?? throw new ArgumentNullException(nameof(peerController));
        }

        public CompatibilityChecker CompatibilityChecker { get; }
        public InstanceId InstanceHash { get; }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var resultType = RecognizeResultType(context);
            var wrapper = new HttpContextHeadersWrapper(context.HttpContext);
            _headersProcessor.AddCommonHeaders(wrapper, resultType);
        }

        private string RecognizeResultType(ActionExecutedContext context)
        {
            string resultType;
            if (context.Result is FileStreamResult)
            {
                resultType = HttpCommonHeadersProcessor.TypeHeaderForStream;
            }
            else if (context.Result is ObjectResult objectResult)
            {
                resultType = objectResult.Value.GetType().Name;
            }
            else
            {
                _logger.LogError($"Unknown response type: {context.Result.GetType().Name}");
                throw new InvalidOperationException("Unknown context type.");
            }

            return resultType;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            CommonHeaderData headerData;
            try
            {
                headerData = _headersProcessor.ReadAndValidateAndProcessCommonHeaders(
                    context.HttpContext.Connection.RemoteIpAddress,
                    new HttpContextHeadersWrapper(context.HttpContext)
                );
            }
            catch(MissingOrInvalidHeaderException headerException)
            {
                context.Result = new ContentResult()
                {
                    Content = headerException.Message,
                    StatusCode = StatusCodes.Status400BadRequest
                };
                return;
            }

            // set for controller
            var controller = (IHttpApiController)context.Controller;
            controller.PeerId = headerData.PeerId;
            controller.IsLoopback = headerData.IsLoopback;
            controller.PeerCatalogVersion = headerData.CatalogVersion;
        }
        
    }
}
