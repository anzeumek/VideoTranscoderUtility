using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;
using VideoTranscoder.Service;

// Register encoding provider for Windows-1250, ISO-8859-2, etc.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient(); //IHttpClientFactory // Add HttpClient factory for making HTTP requests so we will not need to create multiple instances and will not exhaust sockets
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Video Transcoder Service";
});

var host = builder.Build();
host.Run();