using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OnePaySystem.ApiGateway.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class ValidateClientIpMiddleware
    {
        private readonly RequestDelegate _next;
        private string _safeList;
        private readonly ILogger<ValidateClientIpMiddleware> _logger;
        private readonly IConfiguration _configuration;
        private string requestUrl;

        public ValidateClientIpMiddleware(RequestDelegate next, ILogger<ValidateClientIpMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }
        public async Task Invoke(HttpContext httpContext)
        {
            bool checkClientIp = Convert.ToBoolean(_configuration.GetSection("appSettings").GetSection("ValidateClientIp").Value);
            if (checkClientIp)
            {
                _safeList = _configuration.GetSection("appSettings").GetSection("AllowedClientIps").Value;
                requestUrl = httpContext.Request.Path.Value;
                //if (httpContext.Request.Method != HttpMethod.Get.Method) {
                if (requestUrl != "/swagger/index.html" && requestUrl != "/swagger/v1/swagger.json")
                {
                    var remoteIp = httpContext.Connection.RemoteIpAddress;
                    _logger.LogDebug("Request from Remote IP address: {RemoteIp}", remoteIp);

                    string[] ip = _safeList.Split(';');

                    var bytes = remoteIp.GetAddressBytes();
                    var badIp = true;
                    foreach (var address in ip)
                    {
                        var testIp = IPAddress.Parse(address);
                        if (testIp.GetAddressBytes().SequenceEqual(bytes))
                        {
                            badIp = false;
                            break;
                        }
                    }

                    if (badIp)
                    {
                        _logger.LogWarning("Forbidden Request from Remote IP address: {RemoteIp}", remoteIp);
                        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }
                }
            }


            await _next(httpContext);
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class ValidateClientIpMiddlewareExtensions
    {
        public static IApplicationBuilder UseValidateClientIpMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ValidateClientIpMiddleware>();
        }
    }
}
