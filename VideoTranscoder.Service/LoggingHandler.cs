using System;
using System.Collections.Generic;
using System.Text;

namespace VideoTranscoder.Service
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly FileLogger _logger;

        public LoggingHandler(FileLogger logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogToFile("=== HTTP Request ===");
            _logger.LogToFile($"{request.Method} {request.RequestUri}");
            _logger.LogToFile($"Headers: {string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");

            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync();
                _logger.LogToFile($"Body: {body}");
            }

            var response = await base.SendAsync(request, cancellationToken);

            _logger.LogToFile($"Response: {response.StatusCode}");
            return response;
        }
    }
}
