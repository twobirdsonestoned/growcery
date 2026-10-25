using Growcery.Core.Abstractions;
using Growcery.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Growcery.Data;

public static class DependencyInjection
{
    // Registers the EF Core + PostgreSQL implementations of Core's data interfaces.
    public static IServiceCollection AddGrowceryData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IGroceryListRepository, EfGroceryListRepository>();
        services.AddScoped<IPersonRepository, EfPersonRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, PostgresClock>();
        return services;
    }
}
