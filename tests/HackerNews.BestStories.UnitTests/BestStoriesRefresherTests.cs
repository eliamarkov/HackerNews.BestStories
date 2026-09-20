using HackerNews.BestStories.Api;
using HackerNews.BestStories.Api.Dtos;
using HackerNews.BestStories.Api.HackerNewsClient.V0;
using HackerNews.BestStories.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ApiStory = HackerNews.BestStories.Api.Dtos.Story;
using ClientStory = HackerNews.BestStories.Api.HackerNewsClient.V0.Story;

namespace HackerNews.BestStories.UnitTests;

[TestClass]
public sealed class BestStoriesRefresherTests
{
    [TestMethod]
    public async Task StartAsync_WhenStoriesAreReturned_PublishesTopStories()
    {
        var client = Substitute.For<INewsClient>();
        var store = Substitute.For<IBestStorySnapshot>();
        var logger = Substitute.For<ILogger<BestStoriesRefresher>>();
        var options = new HackerNewsOptions
        {
            RefreshPeriod = TimeSpan.FromMinutes(1),
            MaxDegreeOfParallelism = 2
        };

        store.Current.Returns(Array.Empty<ApiStory>());

        client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns([11, 22]);

        client.GetItemAsync(11, Arg.Any<CancellationToken>())
            .Returns(CreateStoryResult(11, "Top Story", "alice", 200, [1, 2], new Uri("https://example.com/1")));

        client.GetItemAsync(22, Arg.Any<CancellationToken>())
            .Returns(CreateStoryResult(22, "Low Story", "bob", 50, [3], new Uri("https://example.com/2")));

        var published = new TaskCompletionSource();
        store.When(s => s.Publish(Arg.Any<ApiStory[]>())).Do(_ => published.TrySetResult());

        using var refresher = new BestStoriesRefresher(client, store, new FakeTimeProvider(), Options.Create(options), logger);

        await refresher.StartAsync(CancellationToken.None);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await refresher.StopAsync(CancellationToken.None);

        store.Received(1).Publish(Arg.Is<ApiStory[]>(stories =>
            stories.Length == 2 &&
            stories[0].Score == 200 &&
            stories[1].Score == 50));
    }

    [TestMethod]
    public async Task StartAsync_WhenRefreshPeriodElapses_RefreshesAgain()
    {
        var client = Substitute.For<INewsClient>();
        var store = Substitute.For<IBestStorySnapshot>();
        var logger = Substitute.For<ILogger<BestStoriesRefresher>>();
        var options = new HackerNewsOptions
        {
            RefreshPeriod = TimeSpan.FromMinutes(1),
            MaxDegreeOfParallelism = 2
        };

        store.Current.Returns(Array.Empty<ApiStory>());
        client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var publishes = new SemaphoreSlim(0);
        store.When(s => s.Publish(Arg.Any<ApiStory[]>()))
            .Do(_ => publishes.Release());

        var timeProvider = new FakeTimeProvider();
        using var refresher = new BestStoriesRefresher(client, store, timeProvider, Options.Create(options), logger);

        await refresher.StartAsync(CancellationToken.None);
        Assert.IsTrue(await publishes.WaitAsync(TimeSpan.FromSeconds(5)), "Initial refresh did not happen.");

        timeProvider.Advance(options.RefreshPeriod);
        Assert.IsTrue(await publishes.WaitAsync(TimeSpan.FromSeconds(5)), "Refresh did not happen after the period elapsed.");

        timeProvider.Advance(options.RefreshPeriod);
        Assert.IsTrue(await publishes.WaitAsync(TimeSpan.FromSeconds(5)), "Refresh did not happen after the second period elapsed.");

        await refresher.StopAsync(CancellationToken.None);

        // the initial load and two timed calls
        await client.Received(3).GetBestStoryIdsAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task StopAsync_WhenServiceIsRunning_StopsByCancellation()
    {
        var client = Substitute.For<INewsClient>();
        var store = Substitute.For<IBestStorySnapshot>();
        var logger = Substitute.For<ILogger<BestStoriesRefresher>>();
        var options = new HackerNewsOptions
        {
            RefreshPeriod = TimeSpan.FromMinutes(1),
            MaxDegreeOfParallelism = 2
        };

        store.Current.Returns(Array.Empty<ApiStory>());
        client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var published = new TaskCompletionSource();
        store.When(s => s.Publish(Arg.Any<ApiStory[]>()))
            .Do(_ => published.TrySetResult());

        using var refresher = new BestStoriesRefresher(client, store, new FakeTimeProvider(), Options.Create(options), logger);

        await refresher.StartAsync(CancellationToken.None);
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await refresher.StopAsync(CancellationToken.None);

        Assert.IsNotNull(refresher.ExecuteTask);
        Assert.IsTrue(refresher.ExecuteTask.IsCanceled);
    }

    private static StoryResult CreateStoryResult(int id, string title, string by, int score, int[] kids, Uri url)
    {
        return new StoryResult.Found(new ClientStory
        {
            Id = id,
            Title = title,
            By = by,
            Score = score,
            Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Type = "story",
            Kids = kids,
            Url = url
        });
    }
}
