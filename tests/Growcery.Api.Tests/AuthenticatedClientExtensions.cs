using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Growcery.Api.Tests;

public static class AuthenticatedClientExtensions
{
    // Production traffic is HTTPS (it ends at the ingress), so tests call the API over https too.
    public static HttpClient CreateHttpsClient(this GrowceryWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public static HttpClient CreateClientWithToken(this GrowceryWebApplicationFactory factory, string token)
    {
        var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // A client for a brand-new user: a unique Auth0-style subject, so tests sharing the InMemory database don't collide.
    public static Task<HttpClient> CreateAuthenticatedClientAsync(this GrowceryWebApplicationFactory factory) =>
        Task.FromResult(factory.CreateClientWithToken(TestTokens.Create($"test|{Guid.NewGuid():N}")));
}
