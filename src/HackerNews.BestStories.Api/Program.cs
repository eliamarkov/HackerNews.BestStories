using HackerNews.BestStories.Api;
using HackerNews.BestStories.Api.HackerNewsClient.V0;
using HackerNews.BestStories.Api.Services;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseSerilog((context, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
    );

builder.Services
    .AddOptions<HackerNewsOptions>()
    .Bind(builder.Configuration.GetSection("HackerNews"))
    .Validate(
        options => options.BaseAddress is
        {
            IsAbsoluteUri: true,
            Scheme: "http" or "https",
            AbsolutePath: "/",
            Query: "",
            Fragment: ""
        },
        "HackerNews:BaseAddress must be an absolute HTTP or HTTPS origin without a path.")
    .ValidateOnStart();

builder.Services
    .AddHttpClient<INewsClient, NewsClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
        client.BaseAddress = options.BaseAddress;
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBestStorySnapshot, BestStorySnapshot>();
builder.Services.AddHostedService<BestStoriesRefresher>();

builder.Services
    .AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
    .WithTitle("Hacker News Best Stories API")
    .WithTheme(ScalarTheme.Purple)
    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseHttpsRedirection();

app.MapGet("/stories/{count:int}", async (
    int count,
    IBestStorySnapshot service,
    CancellationToken ct) =>
{
    var result = service.Current.Take(count);

    return result;
});

app.Run();
