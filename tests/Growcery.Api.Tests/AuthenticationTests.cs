using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Growcery.Api.Contracts;
using Growcery.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Growcery.Api.Tests;

// Spec 003 §4.3: only valid Auth0-style access tokens for this API reach the grocery endpoints.
public class AuthenticationTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly GrowceryWebApplicationFactory _factory;

    public AuthenticationTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string NewSubject() => $"auth0|{Guid.NewGuid():N}";

    private static void AssertBearerChallenge(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
    }

    private async Task<HttpResponseMessage> GetListsWithTokenAsync(string token) =>
        await _factory.CreateClientWithToken(token).GetAsync("/api/lists");

    private async Task<string?> GetDisplayNameAsync(string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var person = await db.People.SingleAsync(p => p.ExternalId == subject);
        return person.DisplayName;
    }

    [Fact]
    public async Task GetLists_WithoutToken_Returns401ProblemDetailsWithBearerChallenge()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/api/lists");

        AssertBearerChallenge(response);
    }

    [Fact]
    public async Task GetLists_WithExpiredToken_Returns401()
    {
        var token = TestTokens.Create(NewSubject(), expires: DateTime.UtcNow.AddMinutes(-5));

        AssertBearerChallenge(await GetListsWithTokenAsync(token));
    }

    [Fact]
    public async Task GetLists_WithWrongAudience_Returns401()
    {
        var token = TestTokens.Create(NewSubject(), audience: "https://some-other-api");

        AssertBearerChallenge(await GetListsWithTokenAsync(token));
    }

    [Fact]
    public async Task GetLists_WithWrongIssuer_Returns401()
    {
        var token = TestTokens.Create(NewSubject(), issuer: "https://another-tenant.auth0.com/");

        AssertBearerChallenge(await GetListsWithTokenAsync(token));
    }

    [Fact]
    public async Task GetLists_WithTokenSignedByOtherKey_Returns401()
    {
        var token = TestTokens.Create(NewSubject(), signingKey: TestTokens.CreateKey("test-key"));

        AssertBearerChallenge(await GetListsWithTokenAsync(token));
    }

    [Fact]
    public async Task GetLists_WithUnsignedToken_Returns401()
    {
        AssertBearerChallenge(await GetListsWithTokenAsync(TestTokens.CreateUnsigned(NewSubject())));
    }

    [Fact]
    public async Task GetLists_WithGarbageToken_Returns401()
    {
        AssertBearerChallenge(await GetListsWithTokenAsync("not-a-jwt"));
    }

    [Fact]
    public async Task GetLists_WithoutTokenAcceptingXml_Returns401NotServerError()
    {
        var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLists_WithValidToken_ReturnsOk()
    {
        var response = await GetListsWithTokenAsync(TestTokens.Create(NewSubject()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadJsonAsync<List<GroceryListSummaryResponse>>());
    }

    [Fact]
    public async Task GetList_WithAnotherUsersToken_ReturnsNotFound()
    {
        var owner = _factory.CreateClientWithToken(TestTokens.Create(NewSubject()));
        var created = await owner.PostAsJsonAsync("/api/lists", new { name = "Mine" });
        var list = await created.Content.ReadJsonAsync<GroceryListResponse>();
        var intruder = _factory.CreateClientWithToken(TestTokens.Create(NewSubject()));

        var response = await intruder.GetAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FirstCall_WithNameClaim_CreatesPersonWithThatDisplayName()
    {
        var subject = NewSubject();

        var response = await GetListsWithTokenAsync(TestTokens.Create(subject, name: "Thandi"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Thandi", await GetDisplayNameAsync(subject));
    }

    [Fact]
    public async Task FirstCall_WithoutNameClaim_UsesSubjectAsDisplayName()
    {
        var subject = NewSubject();

        var response = await GetListsWithTokenAsync(TestTokens.Create(subject));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(subject, await GetDisplayNameAsync(subject));
    }

    [Theory]
    [InlineData("/api/account/login")]
    [InlineData("/api/account/register")]
    public async Task AccountEndpoints_AreRemoved_ReturnNotFound(string route)
    {
        var response = await _factory.CreateHttpsClient().PostAsJsonAsync(route, new { userName = "a", password = "b" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
