using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network
{
    public class HttpFormatter : IInputFormatter, IOutputFormatter
    {
        private readonly IMessageSerializer serializer;

        public HttpFormatter(IMessageSerializer serializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        bool IInputFormatter.CanRead(InputFormatterContext context)
        {
            if (!(context.HttpContext.Request.ContentType ?? "").Equals(serializer.MimeType, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        bool IOutputFormatter.CanWriteResult(OutputFormatterCanWriteContext context)
        {
            return true;
        }

        Task<InputFormatterResult> IInputFormatter.ReadAsync(InputFormatterContext context)
        {
            object result = serializer.Deserialize(context.HttpContext.Request.Body, context.ModelType);
            return InputFormatterResult.SuccessAsync(result);
        }

        Task IOutputFormatter.WriteAsync(OutputFormatterWriteContext context)
        {
            context.ContentType = serializer.MimeType;
            serializer.Serialize(context.Object, context.HttpContext.Response.Body);
            return Task.CompletedTask;
        }
    }
}
