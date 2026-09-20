using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

// Program.cs
builder.Services.AddHttpClient<INewsClient, NewsClient>((serviceProvider, client) =>
{
    var o = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = o.BaseAddress;
}); 

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
