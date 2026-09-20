using HackerNews.BestStories.Api;
using HackerNews.BestStories.Api.HackerNewsClient.V0;
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
        var o = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
        client.BaseAddress = o.BaseAddress;
    });

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

app.MapGet("/stories/{id:int}", async (
    int id,
    INewsClient newsClient,
    CancellationToken ct) =>
{
    var result = await newsClient.GetItemAsync(id, ct);

    return result switch
    {
        StoryResult.Found found => Results.Ok(found.Story),
        StoryResult.NotFound => Results.NotFound(),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
    };
});

app.Run();
