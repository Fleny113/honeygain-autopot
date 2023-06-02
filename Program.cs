using System.Net.Http.Headers;
using HoneyGainAutoRewardPot;
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

builder.ConfigureServices((context, services) =>
{
    services.AddOptions<HoneyGainApplicationSettings>()
        .BindConfiguration(HoneyGainApplicationSettings.SectionName);

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
