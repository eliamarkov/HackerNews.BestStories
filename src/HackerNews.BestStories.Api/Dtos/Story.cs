namespace HackerNews.BestStories.Api.Dtos;

public record Story(
    string Title,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount,
    Uri Uri);