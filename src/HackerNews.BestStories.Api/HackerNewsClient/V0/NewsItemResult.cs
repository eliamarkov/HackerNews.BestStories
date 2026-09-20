namespace HackerNews.BestStories.Api.HackerNewsClient.V0;

public abstract record StoryResult
{
    private StoryResult() { }
    public sealed record Found(Story Story) : StoryResult;
    public sealed record NotFound(int Id) : StoryResult;
}