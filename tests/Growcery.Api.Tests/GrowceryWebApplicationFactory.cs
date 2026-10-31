using System.Linq;
using Growcery.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Growcery.Api.Tests;

public class GrowceryWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public GrowceryWebApplicationFactory()
    {
        // Program.cs reads the connection string via builder.Configuration before any
        // WebApplicationFactory configuration hooks get a chance to run, so it must be
        // supplied as a process environment variable rather than via ConfigureAppConfiguration.
        // The real Npgsql-backed DbContext registration it feeds is swapped out for EF InMemory below.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=growcery_test;Username=test;Password=test");

        // Program.cs requires the Auth0 settings at startup. Token validation is then pointed at the test key below.
        Environment.SetEnvironmentVariable("Auth0__Domain", "test.growcery.local");
        Environment.SetEnvironmentVariable("Auth0__Audience", TestTokens.Audience);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers the Npgsql configuration both as DbContextOptions<ApplicationDbContext>
            // and as a separate IDbContextOptionsConfiguration<ApplicationDbContext> entry; removing only the
            // former leaves the latter in place, so both providers end up configured on the same options.
            var applicationDbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                    || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(ApplicationDbContext))))
                .ToList();
            foreach (var descriptor in applicationDbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Validate tokens against the test key instead of downloading Auth0's signing keys, so tests make no
            // network calls. The rest of Program.cs's validation settings (algorithms, claim names) still apply.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null;
                options.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
                options.TokenValidationParameters.ValidAudience = TestTokens.Audience;
                options.TokenValidationParameters.IssuerSigningKey = TestTokens.SigningKey;
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
