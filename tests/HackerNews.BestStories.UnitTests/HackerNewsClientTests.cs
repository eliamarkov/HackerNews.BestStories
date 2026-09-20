using RichardSzalay.MockHttp ;
using System.Net;
using System.Net.Http.Json;

namespace HackerNews.BestStories.UnitTests;

[TestClass]
public sealed class HackerNewsClientTests
{
    const int Id = 123;
    private static readonly Uri BaseAddress = new("https://test.com/");
    private static string TestUri = new Uri(BaseAddress, $"v0/item/{Id}.json").ToString();

    [TestMethod]
    public async Task GetItemAsync_WhenItemDoesNotExist_ReturnsNotFound()
    {
        using var httpClient = CreateMockHttpClient(mockHttp => 
            mockHttp.When(TestUri)
                .Respond("application/json", "null")
        );

        var client = new NewsClient(httpClient);

        var result = await client.GetItemAsync(Id, CancellationToken.None);

        var notFound = result as StoryResult.NotFound;

        Assert.IsNotNull(notFound);
        Assert.AreEqual(Id, notFound.Id);
    }

    [TestMethod]
    public async Task GetItemAsync_WhenItemExist_ReturnsStory()
    {
        var testStory = new Story { Id = Id, Title = "Test Story" };
        using var httpClient = CreateMockHttpClient(mockHttp => 
            mockHttp.When(TestUri)
                .Respond(() => Task.FromResult(new HttpResponseMessage
                {
                    Content = JsonContent.Create(testStory)
                }))
        );

        var client = new NewsClient(httpClient);

        var result = await client.GetItemAsync(Id, CancellationToken.None);

        var story = (result as StoryResult.Found)?.Story;
        
        Assert.IsNotNull(story);
        Assert.AreEqual(Id, story.Id);
        Assert.AreEqual(testStory.Title, story.Title);
    }

    [TestMethod]
    public async Task GetItemAsync_When_HTTP_Error_Throws()
    {
        using var httpClient = CreateMockHttpClient(mockHttp => 
            mockHttp.When(TestUri)
                .Respond(HttpStatusCode.InternalServerError)
        );

        var client = new NewsClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetItemAsync(Id, CancellationToken.None));
    }

    private static HttpClient CreateMockHttpClient(Action<MockHttpMessageHandler> configure)
    {
        var mockHttp = new MockHttpMessageHandler();

        configure(mockHttp);

        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = BaseAddress;

        return httpClient;
    }
}
