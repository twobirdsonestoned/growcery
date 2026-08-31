using System.Net;
using System.Net.Http.Json;
using growcery.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace growcery.Tests;

public class AccountControllerLoginTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private readonly GrowceryWebApplicationFactory _factory;

    public AccountControllerLoginTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task CreateUserAsync(string userName, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = userName };
        var result = await userManager.CreateAsync(user, password);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndSetCookieHeader()
    {
        const string userName = "valid-login-user";
        const string password = "P@ssw0rd123!";
        await CreateUserAsync(userName, password);

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/login", new LoginViewModel
        {
            UserName = userName,
            Password = password,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "Expected a Set-Cookie header on a successful login response.");
        Assert.Contains(cookies!, cookie => cookie.StartsWith(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedAndNoSetCookieHeader()
    {
        const string userName = "invalid-login-user";
        const string password = "P@ssw0rd123!";
        await CreateUserAsync(userName, password);

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/account/login", new LoginViewModel
        {
            UserName = userName,
            Password = "WrongPassword!",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _), "Did not expect a Set-Cookie header on a failed login response.");
    }
}
