using System.Net.Http.Headers;
using HoneyGainAutoRewardPot;
using Microsoft.Extensions.Options;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging((ctx, loggingBuilder) =>
{
    loggingBuilder.AddConfiguration(ctx.Configuration.GetSection("Logging"));
    loggingBuilder.AddFile(o =>
    {
        o.RootPath = Environment.CurrentDirectory;
    });
});


builder.ConfigureServices((context, services) =>
{
    services.Configure<HoneyGainApplicationSettings>(context.Configuration.GetSection(HoneyGainApplicationSettings.SectionName));
    
    services.AddHttpClient("HoneyGain", (serviceProvider, client) =>
    {
        var settings = serviceProvider.GetRequiredService<IOptions<HoneyGainApplicationSettings>>();

        client.BaseAddress = new Uri("https://dashboard.honeygain.com/api/v1/");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.Token);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.Value.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    });
    
    services.AddHostedService<Worker>();
});

var app = builder.Build();

app.Run();
