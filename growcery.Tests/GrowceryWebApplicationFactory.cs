using System.Linq;
using growcery.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace growcery.Tests;

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

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
