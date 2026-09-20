public sealed class Story
{
    public string? By { get; init; }
    public int Descendants { get; init; }
    public int Id { get; init; }
    public int[]? Kids { get; init; }
    public int Score { get; init; }
    public long Time { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public Uri? Url { get; init; }
}

