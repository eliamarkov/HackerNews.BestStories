namespace HackerNews.BestStories.Api;

public class HackerNewsOptions
{
	public Uri BaseAddress { get; init; } = null!;
	
	public TimeSpan RefreshPeriod { get; init; } = TimeSpan.FromMinutes(1);

	public int MaxDegreeOfParallelism { get; init; } = 10;
}