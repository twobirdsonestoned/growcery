using Growcery.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Growcery.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();

    public DbSet<GroceryList> GroceryLists => Set<GroceryList>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Domain entities are mapped in Configurations/ so Growcery.Core stays free of persistence attributes.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
