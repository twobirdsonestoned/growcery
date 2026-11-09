using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace Growcery.Api.Tests;

// Spec 003 §4.4: Swagger UI's Authorize button logs in through Auth0 and sends the token on every call.
public class SwaggerTests : IClassFixture<GrowceryWebApplicationFactory>
{
    private readonly GrowceryWebApplicationFactory _factory;

    public SwaggerTests(GrowceryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private OpenApiDocument GetDocument() =>
        _factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

    [Fact]
    public void SwaggerDocument_DeclaresAuth0AuthorizationCodeFlow()
    {
        var document = GetDocument();

        var scheme = Assert.Contains("auth0", document.Components!.SecuritySchemes!);
        Assert.Equal(SecuritySchemeType.OAuth2, scheme.Type);
        var flow = scheme.Flows!.AuthorizationCode!;
        Assert.Equal(new Uri("https://test.growcery.local/authorize"), flow.AuthorizationUrl);
        Assert.Equal(new Uri("https://test.growcery.local/oauth/token"), flow.TokenUrl);
    }

    [Fact]
    public void SwaggerDocument_RequiresAuth0SchemeForAllOperations()
    {
        var document = GetDocument();

        var requirement = Assert.Single(document.Security!);
        var reference = Assert.Single(requirement.Keys);
        Assert.Equal("auth0", reference.Reference.Id);
    }
}
