using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ShareCluster.Network.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Provides serialization and deserialization of messages for ASP.NET MVC.
    /// </summary>
    public class HttpFormatter : IInputFormatter, IOutputFormatter
    {
        bool IInputFormatter.CanRead(InputFormatterContext context)
        {
            IMessageSerializer serializer = context.HttpContext.RequestServices.GetRequiredService<IMessageSerializer>();

            if (!(context.HttpContext.Request.ContentType ?? "").Equals(serializer.MimeType, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!context.HttpContext.Request.Headers.TryGetValue(HttpRequestHeaderValidator.TypeHeaderName, out StringValues typeHeadValues))
                return false;

            if (context.ModelType.Name != typeHeadValues.ToString())
            {
                return false;
            }

            return true;
        }

        bool IOutputFormatter.CanWriteResult(OutputFormatterCanWriteContext context)
        {
            bool isAssignable = typeof(IMessage).IsAssignableFrom(context.ObjectType);
            return isAssignable;
        }

        Task<InputFormatterResult> IInputFormatter.ReadAsync(InputFormatterContext context)
        {
            IMessageSerializer serializer = context.HttpContext.RequestServices.GetRequiredService<IMessageSerializer>();

            object result = serializer.Deserialize(context.HttpContext.Request.Body, context.ModelType);
            return InputFormatterResult.SuccessAsync(result);
        }

        Task IOutputFormatter.WriteAsync(OutputFormatterWriteContext context)
        {
            IMessageSerializer serializer = context.HttpContext.RequestServices.GetRequiredService<IMessageSerializer>();
            CompatibilityChecker compatibilityChecker = context.HttpContext.RequestServices.GetRequiredService<CompatibilityChecker>();
            InstanceHash instanceHash = context.HttpContext.RequestServices.GetRequiredService<InstanceHash>();

            // add headers
            context.HttpContext.Response.Headers.Add(HttpRequestHeaderValidator.TypeHeaderName, context.ObjectType.Name);
            context.HttpContext.Response.Headers.Add(HttpRequestHeaderValidator.VersionHeaderName, compatibilityChecker.NetworkProtocolVersion.ToString());
            context.HttpContext.Response.Headers.Add(HttpRequestHeaderValidator.InstanceHeaderName, instanceHash.Hash.ToString());
            context.ContentType = serializer.MimeType;

            // serialize
            serializer.Serialize(context.Object, context.HttpContext.Response.Body, context.ObjectType);
            return Task.CompletedTask;
        }
    }
}
