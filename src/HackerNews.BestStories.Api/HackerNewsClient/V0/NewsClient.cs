public sealed class NewsClient(HttpClient http) : INewsClient
{
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync("v0/beststories.json", ClientJsonContext.Default.Int32Array, ct) ?? [];

    public async Task<StoryResult> GetItemAsync(int id, CancellationToken ct)
    {
        var item = await http.GetFromJsonAsync($"v0/item/{id}.json", ClientJsonContext.Default.Story, ct);

        return item switch
        {
            null => new StoryResult.NotFound(id),
            _ => new StoryResult.Found(item)
        };
    }
}