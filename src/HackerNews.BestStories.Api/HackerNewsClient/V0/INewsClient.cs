namespace HackerNews.BestStories.Api.HackerNewsClient.V0;

public interface INewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct);
    Task<StoryResult> GetItemAsync(int id, CancellationToken ct);
}
