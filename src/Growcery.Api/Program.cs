using System.Text.Json.Serialization;
using Growcery.Api.Infrastructure;
using Growcery.Core;
using Growcery.Core.Abstractions;
using Growcery.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
    {
        // A malformed field already produces its own error; don't add a second, misleading "request is required" one.
        // An empty body is still rejected by [ApiController].
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

        // Don't leak internal type names from JSON parsing errors to clients.
        options.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.AddProblemDetails();

// Identity lives in Auth0 (spec 003). The API only validates the access tokens Auth0 issues for it.
var auth0 = builder.Configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>();
if (string.IsNullOrWhiteSpace(auth0?.Domain) || string.IsNullOrWhiteSpace(auth0.Audience))
{
    throw new InvalidOperationException(
        "Auth0:Domain and Auth0:Audience must be configured. See 'Auth0 setup' in README.md.");
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Growcery API",
        Version = "v1"
    });

    // Swagger UI's Authorize button logs in on Auth0's hosted page (Authorization Code + PKCE) and then sends the
    // access token on every call.
    options.AddSecurityDefinition("auth0", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://{auth0.Domain}/authorize"),
                TokenUrl = new Uri($"https://{auth0.Domain}/oauth/token"),
                Scopes = new Dictionary<string, string>(),
            },
        },
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("auth0", document)] = [],
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Composition root: the business layer, the data layer that implements its interfaces, and the current user.
builder.Services.AddGrowceryCore();
builder.Services.AddGrowceryData(connectionString);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Signing keys are downloaded from the tenant's JWKS endpoint and cached.
        options.Authority = $"https://{auth0.Domain}/";
        options.Audience = auth0.Audience;

        // Keep JWT claim names as issued ("sub"), instead of mapping them to WS-* claim types.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = Auth0Options.NameClaim;
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages(context =>
    Results.Problem(statusCode: context.HttpContext.Response.StatusCode)
        .ExecuteAsync(context.HttpContext));

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // A public client: no secret, PKCE instead. The audience makes Auth0 issue an access token for this API.
        options.OAuthClientId(auth0.SwaggerClientId);
        options.OAuthUsePkce();
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string> { ["audience"] = auth0.Audience });
    });
}


app.Run();

public partial class Program { }
