using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebAtol.AtolApi.Models;

namespace WebAtol.AtolApi
{
    public class AtolClientBase
    {
        private readonly IHttpClientFactory _cli;
        private readonly ILogger<AtolClientBase> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public static JsonSerializerOptions JsonOptions => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public AtolClientBase(IHttpClientFactory cli, ILogger<AtolClientBase> logger, IHttpContextAccessor httpContextAccessor)
        {
            _cli = cli;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public bool ThrowOnErrors { get; set; } = false;


        protected async Task<string> SendRequestRaw(Uri baseUri, string url, HttpMethod method, object body = null)
        {
            var httpCli = _cli.CreateClient();
            httpCli.BaseAddress = baseUri;

            var req = new HttpRequestMessage(method, url);

            _logger.LogDebug($"Atol request: {method} {baseUri} {url}");

            if (body != null)
            {
                var bodyRaw = JsonSerializer.Serialize(body, JsonOptions);

                _logger.LogDebug($"{bodyRaw}");

                req.Content = new StringContent(bodyRaw, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage res;

            try
            {
                res = await httpCli.SendAsync(req);
            }
            catch (Exception e)
            {
                _logger.LogError($"Atol connect: {e.Message}");
                throw new ApiExc(ErrorEnum.ERR_ATOL_WEB_CONNECT, e.Message);
            }

            _httpContextAccessor.HttpContext?.Response.Headers.TryAdd("X-ORIGINAL-ATOL-STATUS-CODE", ((int)res.StatusCode).ToString());

            var raw = await res.Content.ReadAsStringAsync();

            _logger.LogDebug($"Response {(int)res.StatusCode} {raw}");

            if (ThrowOnErrors)
            {
                if (res.StatusCode != HttpStatusCode.OK && res.StatusCode != HttpStatusCode.Created)
                {
                    AtolError atolError = null;

                    try
                    {
                        atolError = JsonSerializer.Deserialize<AtolError>(raw, JsonOptions);
                    }
                    catch (Exception e)
                    {
                        //
                    }

                    if (atolError != null)
                    {
                        throw new ApiExc(ErrorEnum.ERR_ATOL_INTERNAL, atolError.Error.Description);
                    }
                }
            }

            return raw;

        }
    }
}
