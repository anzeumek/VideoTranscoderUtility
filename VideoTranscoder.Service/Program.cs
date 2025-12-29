using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoTranscoder.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Video Transcoder Service";
});

var host = builder.Build();
host.Run();