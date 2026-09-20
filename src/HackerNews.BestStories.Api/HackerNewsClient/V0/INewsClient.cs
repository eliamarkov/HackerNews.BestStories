public interface INewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct);
    Task<StoryResult> GetItemAsync(int id, CancellationToken ct);
}
