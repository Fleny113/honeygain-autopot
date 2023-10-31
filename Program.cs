using System.Net.Http.Headers;
using HoneyGainAutoPot;
using Microsoft.Extensions.Options;
using dotenv.net;

DotEnv.Load();

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging((ctx, loggingBuilder) =>
{
    loggingBuilder.AddConfiguration(ctx.Configuration.GetSection("Logging"));
    loggingBuilder.AddFile(o =>
    {
        o.RootPath = Environment.CurrentDirectory;
    });
});

builder.ConfigureServices(services =>
{
    services.AddOptions<HoneyGainApplicationSettings>()
        .BindConfiguration(HoneyGainApplicationSettings.SectionName)
        .Validate(settings => settings is { Token: not null, WebhookUrl: not null });
    
    services.AddHttpClient("HoneyGain", (serviceProvider, client) =>
    {
        var settings = serviceProvider.GetRequiredService<IOptions<HoneyGainApplicationSettings>>();

        client.BaseAddress = new Uri("https://dashboard.honeygain.com/api/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Value.Token);
    });

    services.AddHostedService<RewardPotClaimer>();
});

var app = builder.Build();

app.Run();
