using HackerNews.BestStories.Api.HackerNewsClient.V0;
using Microsoft.Extensions.Options;
using StoryDto = HackerNews.BestStories.Api.Dtos.Story;

namespace HackerNews.BestStories.Api.Services;

public sealed partial class BestStoriesRefresher(
    INewsClient client,
    IBestStorySnapshot store,
    TimeProvider timeProvider,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RefreshPeriod, timeProvider);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            var ids = await client.GetBestStoryIdsAsync(ct);
            var previous = store.Current;
            var results = new StoryDto?[ids.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, ids.Count),
                new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxDegreeOfParallelism, CancellationToken = ct },
                async (index, token) => results[index] = await GetStoryAsync(ids[index], ct));

            var stories = results.OfType<StoryDto>()
                .OrderByDescending(s => s.Score)
                .ToArray();

            store.Publish(stories);
            LogRefreshed(stories.Length, ids.Count, timeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogTermination();
            throw;
        }
        catch (Exception ex)
        {
            LogRefreshFailed(ex);
        }
    }

    async Task<StoryDto?> GetStoryAsync(int id, CancellationToken ct)
    {
        return await client.GetItemAsync(id, ct) switch
        {
            StoryResult.Found(var item) => new StoryDto(
                item.Title ?? string.Empty,
                item.By ?? string.Empty,
                DateTimeOffset.FromUnixTimeSeconds(item.Time),
                item.Score,
                item.Kids?.Length ?? 0,
                item.Url ?? new Uri("https://example.invalid")),
            StoryResult.NotFound => null,
            _ => null
        };        
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Refreshed {StoryCount} of {IdCount} best stories in {ElapsedMs:F0} ms")]
    private partial void LogRefreshed(int storyCount, int idCount, double elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Shutting down the service")]
    private partial void LogTermination();


    [LoggerMessage(
    Level = LogLevel.Error,
    Message = "Failed to refresh best stories")]
    private partial void LogRefreshFailed(Exception exception);
}