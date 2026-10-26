using Growcery.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Growcery.Core;

public static class DependencyInjection
{
    // Registers the business services. The host must also register implementations of the
    // Abstractions interfaces (the data layer and the presentation layer's ICurrentUser).
    public static IServiceCollection AddGrowceryCore(this IServiceCollection services)
    {
        services.AddScoped<IGroceryListService, GroceryListService>();
        services.AddScoped<IGroceryListItemService, GroceryListItemService>();
        return services;
    }
}
