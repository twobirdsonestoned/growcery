using System.Net.Http.Json;
using growcery.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace growcery.Tests;

public static class AuthenticatedClientExtensions
{
    public const string Password = "P@ssw0rd123!";

    // The auth cookie is Secure outside Development, and HttpClient only sends Secure cookies over https.
    public static HttpClient CreateHttpsClient(this GrowceryWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this GrowceryWebApplicationFactory factory)
    {
        var client = factory.CreateHttpsClient();
        var userName = $"user-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/account/register", new RegisterViewModel
        {
            UserName = userName,
            Password = Password,
            ConfirmPassword = Password,
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return client;
    }
}
