using System.Net.Http.Headers;
using HoneyGainAutoRewardPot;

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
    services.AddHttpClient("HoneyGain", (serviceProvider, client) =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        
        var settings = new HoneyGainApplicationSettings();
        configuration.GetSection(HoneyGainApplicationSettings.SectionName).Bind(settings);

        client.BaseAddress = new Uri("https://dashboard.honeygain.com/api/v1/");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    });
    
    services.AddHostedService<Worker>();
});

var app = builder.Build();

app.Run();
