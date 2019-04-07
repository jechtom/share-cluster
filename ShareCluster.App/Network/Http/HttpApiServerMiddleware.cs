using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Messages;
using ShareCluster.Packaging;
using ShareCluster.Packaging.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    public class HttpApiServerMiddleware
    {
        private readonly RequestDelegate _next;

        public HttpApiServerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IApiService apiService, HttpCommonHeadersProcessor headersProcessor, IMessageSerializer serializer)
        {
            switch (context.Request.Path)
            {
                case HttpApiConstants.UrlGetCatalog:
                    CatalogDataRequest catalogRequest = DeserializeRequest<CatalogDataRequest>(serializer, headersProcessor, context);
                    CatalogDataResponse catalogResponse = apiService.GetCatalog(catalogRequest);
                    await SerializeProtoResponseAsync(serializer, headersProcessor, context, catalogResponse);
                    return;
                case HttpApiConstants.UrlGetPackage:
                    PackageRequest packageRequest = DeserializeRequest<PackageRequest>(serializer, headersProcessor, context);
                    PackageResponse packageResponse = apiService.GetPackage(packageRequest);
                    await SerializeProtoResponseAsync(serializer, headersProcessor, context, packageResponse);
                    return;
                case HttpApiConstants.UrlGetData:
                    DataRequest dataRequest = DeserializeRequest<DataRequest>(serializer, headersProcessor, context);
                    (DataResponseSuccess dataSuccess, DataResponseFault dataFault) = apiService.GetDataStream(dataRequest);
                    if (dataFault != null)
                    {
                        await SerializeProtoResponseAsync(serializer, headersProcessor, context, dataFault);
                        return;
                    }
                    // send data stream
                    await WriteDataStreamAsync(context, headersProcessor, dataSuccess);
                    return;
            }

            await _next(context);
        }

        private static async Task WriteDataStreamAsync(HttpContext context, HttpCommonHeadersProcessor headersProcessor, DataResponseSuccess dataSuccess)
        {
            try
            {
                var wrapper = new HttpContextHeadersWrapper(context);
                headersProcessor.AddCommonHeaders(wrapper, HttpCommonHeadersProcessor.TypeHeaderForStream);
                headersProcessor.AddSegmentsHeader(wrapper, dataSuccess.SegmentsInStream);

                context.Response.ContentType = "application/octet-stream";
                await dataSuccess.Stream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            finally
            {
                dataSuccess.Stream.Dispose();
            }
        }

        private async Task SerializeProtoResponseAsync<T>(IMessageSerializer serializer, HttpCommonHeadersProcessor headersProcessor, HttpContext context, T response) where T : IMessage
        {
            var wrapper = new HttpContextHeadersWrapper(context);
            headersProcessor.AddCommonHeaders(wrapper, typeof(T).Name);

            context.Response.ContentType = serializer.MimeType;
            using (var memStream = new MemoryStream())
            {
                serializer.Serialize<T>(response, memStream);
                memStream.Position = 0;
                context.Response.ContentLength = memStream.Length;
                await memStream.CopyToAsync(context.Response.Body);
            }
        }

        private T DeserializeRequest<T>(IMessageSerializer serializer, HttpCommonHeadersProcessor headersProcessor, HttpContext context) where T : IMessage
        {
            CommonHeaderData headerData;
            try
            {
                headerData = headersProcessor.ReadAndValidateAndProcessCommonHeaders(
                    context.Connection.RemoteIpAddress,
                    PeerCommunicationDirection.TcpIncoming,
                    new HttpContextHeadersWrapper(context)
                );
            }
            catch (MissingOrInvalidHeaderException)
            {
                throw; // bad request
            }
            
            // deserialize
            if (context.Request.ContentType != serializer.MimeType)
            {
                throw new InvalidOperationException($"Invalid content type. Expected {serializer.MimeType} but got {context.Request.ContentType}.");
            }

            T result = serializer.Deserialize<T>(context.Request.Body);
            if (result == null) throw new InvalidOperationException("Cannot deserialize request. Result is null.");
            return result;
        }
    }
}
