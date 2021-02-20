﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WhipStat.Helpers
{
    public static class WebClient
    {
        private static readonly HttpClient client = new HttpClient(new RetryHandler());
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions();

        public static async Task<T> SendAsync<T>(HttpMethod method, Uri url, string authorization = null, object body = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("Accept", "application/json");
            if (authorization != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization);
            if (body != null)
            {
                HttpContent content = body as HttpContent;
                if (content != null)
                {
                    request.Content = content;
                }
                else
                {
                    string json = JsonSerializer.Serialize(body, options);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
            }

            var response = await client.SendAsync(request, CancellationToken.None);

            if (typeof(T) == typeof(HttpResponseMessage))
                return (T)(object)response;

            string responseBody = "{}";
            if (response.Content != null)
            {
                // sometimes returning new HttpResponseMessage() may yield no body. 
                responseBody = await response.Content.ReadAsStringAsync();
            }

            ThrowIfFailed(method, url, response, responseBody);

            return JsonSerializer.Deserialize<T>(responseBody);
        }

        private static void ThrowIfFailed(HttpMethod method, Uri url, HttpResponseMessage response, string body)
        {
            if (response.IsSuccessStatusCode && !body.StartsWith("An error"))   // TODO: Remove hack when success code isn't returned for failure
                return;

            throw new InvalidOperationException($"Call to {method} {url} failed with {response.StatusCode}. Body={body}");
        }
    }

    public class RetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;

        public RetryHandler() : base(new HttpClientHandler()) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (int i = 0; i < MaxRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return response;
            }

            return response;
        }
    }
}
