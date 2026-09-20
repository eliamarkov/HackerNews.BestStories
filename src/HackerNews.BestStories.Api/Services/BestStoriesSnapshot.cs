using HackerNews.BestStories.Api.Dtos;

namespace HackerNews.BestStories.Api.Services;

public class BestStorySnapshot : IBestStorySnapshot
{
    private Story[] current = [];

    public IReadOnlyList<Story> Current => Volatile.Read(ref current);
    public void Publish(Story[] snapshot) => Volatile.Write(ref current, snapshot);

    public bool TryGetBest(int n, out IReadOnlyList<Story> stories)
    {
        var snapshot = Current;
        if (snapshot is null) {
            stories = [];
            return false;
        }

        stories = snapshot.Take(n).ToList();
        return true;
    }    
}