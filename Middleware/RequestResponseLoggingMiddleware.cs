using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OnePaySystem.Services.Interface;
using Common.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnePaySystem.Models.DTOs;
using Newtonsoft.Json;

namespace ApiGateway.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class RequestResponseLoggingMiddleware
    {
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
        private readonly RequestDelegate _next;
        private const string JsonContentType = "application/json";
        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            
        }

        public async Task InvokeAsync(HttpContext context, IBilling billing, IConfiguration configuration)
        {
            context.Request.EnableBuffering();

            var builder = new StringBuilder();
            var gatewayBasePaths = configuration.GetSection("appSettings").GetSection("GatewayBasePaths").Value.Split(';');
            var request = await FormatRequest(context.Request);
            var divider = "://";
            var url = context.Request.Scheme.Trim() + divider + context.Request.Host.ToString().Trim() + context.Request.Path.ToString().Trim() + context.Request.QueryString.ToString().Trim();
            var apiRoute = context.Request.Path.ToString();
            foreach (var basePath in gatewayBasePaths)
            {
                if (!string.IsNullOrEmpty(basePath))
                {
                    apiRoute = apiRoute.Replace(basePath, "");
                }
                
            }
            //Todo Get Configuration Using Route
            _logger.LogInformation($"Request Url {url}");
            builder.Append("Request: ").AppendLine(request);
            var formattedRequest = request.Split('~');
            var requestBody = formattedRequest[1];
            _logger.LogInformation($"Request Body  {requestBody}");
            builder.AppendLine("Request headers:");
            var bearerToken = "";
            foreach (var header in context.Request.Headers)
            {
                builder.Append(header.Key).Append(':').AppendLine(header.Value);
                if (header.Key == "Authorization")
                    bearerToken = header.Value;
            }
            bearerToken = bearerToken.Replace("Bearer", "").Replace("bearer", "").Trim();
            _logger.LogInformation($"Bearer Token {bearerToken}");
            var clientId = "";
            if(!string.IsNullOrEmpty(bearerToken))
                clientId = Computation.GetUserIdFromToken(bearerToken);

            //Todo validate billing and store store request data for billing
            var validationResponse = new ClientValidationResponse();
            var message = "Processing Failed";
            if (apiRoute != "/token/connect/token" && apiRoute != "/connect/token" && !string.IsNullOrEmpty(clientId))
            {
                
                //Todo Validations and Calculations

                //Todo Get Client Configuration using clientId
                //Todo Get API configuration using route
                //Todo Derive Billing Amount using the above 
                //Todo Validate that client has enough funds to finish the call

                //When Validation Fails
                if (string.IsNullOrEmpty(clientId))
                {
                    message = "Invalid Client";
                    FailedHeaders(context, message);
                    return;
                }


                
            }

            //Copy a pointer to the original response body stream
            var originalBodyStream = context.Response.Body;
            
            //Create a new memory stream...
            using var responseBody = new MemoryStream();
            //...and use that for the temporary response body
            context.Response.Body = responseBody;

            //Continue down the Middleware pipeline, eventually returning to this class
            await _next(context);

            //Format the response from the server
            var statusCode = context.Response.StatusCode;
            _logger.LogInformation($"Status Code {statusCode}");
            var response = await FormatResponse(context.Response);
            var formattedResponse = response.Split('~');
            var responseBod = formattedResponse[1].Trim();
            _logger.LogInformation($"Response Body {responseBod}");
            builder.Append("Response: ").AppendLine(response);
            builder.AppendLine("Response headers: ");

            if (apiRoute != "/token/connect/token")
            {
                //Todo Billing
                validationResponse.StatusCode = statusCode;
                validationResponse.RequestJson = requestBody;
                validationResponse.ResponseJson = responseBod;
                try
                {
                    //await billing.PublishBilling(validationResponse);
                }
                catch (Exception e)
                {
                    _logger.LogError(JsonConvert.SerializeObject(e));
                }
                
            }

            foreach (var header in context.Response.Headers)
            {
                builder.Append(header.Key).Append(':').AppendLine(header.Value);
            }

            //Save log to chosen datastore
            _logger.LogInformation(builder.ToString());

            //Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
            await responseBody.CopyToAsync(originalBodyStream);
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            // Leave the body open so the next middleware can read it.
            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            // Do some processing with body…

            var formattedRequest = $"{request.Scheme} {request.Host}{request.Path} {request.QueryString} ~ {body}";

            // Reset the request body stream position so the next middleware can read it
            request.Body.Position = 0;

            return formattedRequest;
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            //We need to read the response stream from the beginning...
            response.Body.Seek(0, SeekOrigin.Begin);

            //...and copy it into a string
            string text = await new StreamReader(response.Body).ReadToEndAsync();

            //We need to reset the reader for the response so that the client can read it.
            response.Body.Seek(0, SeekOrigin.Begin);

            //Return the string for the response, including the status code (e.g. 200, 404, 401, etc.)
            return $"{response.StatusCode}~ {text}";
        }

        private static void FailedHeaders(HttpContext context, string message = "Request Processing Failed")
        {
            var httpStatusCode = StatusCodes.Status400BadRequest;

            // set http status code and content type
            context.Response.StatusCode = httpStatusCode;
            context.Response.ContentType = JsonContentType;
            context.Response.WriteAsync(
                JsonConvert.SerializeObject(new ErrorModelViewModel
                {
                    message = message
                }));

            // writes / returns error model to the response
            context.Response.OnStarting(state => {
                var httpContext = (HttpContext)state;

                return Task.CompletedTask;
            }, context);

            context.Response.Headers.Clear();
        }

        internal class ErrorModelViewModel
        {
            public object message { get; set; }
        }
    }
}
