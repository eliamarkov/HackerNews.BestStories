namespace HackerNews.BestStories.Api.HackerNewsClient.V0;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(Story))]
internal sealed partial class ClientJsonContext : JsonSerializerContext;