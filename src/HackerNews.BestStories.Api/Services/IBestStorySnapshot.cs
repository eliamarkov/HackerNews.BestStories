using HackerNews.BestStories.Api.Dtos;

namespace HackerNews.BestStories.Api.Services;

public interface IBestStorySnapshot
{
    IReadOnlyList<Story> Current { get; }

    void Publish(Story[] stories);
}