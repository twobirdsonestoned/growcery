using Growcery.Data.Entities;
using Growcery.Data.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Growcery.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<GroceryList> GroceryLists => Set<GroceryList>();

    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GroceryList>(list =>
        {
            list.Property(l => l.OwnerId).IsRequired();
            list.Property(l => l.Name).IsRequired().HasMaxLength(100);

            list.HasIndex(l => l.OwnerId);
            list.HasIndex(l => l.DeletedAt);

            list.HasOne(l => l.Owner)
                .WithMany()
                .HasForeignKey(l => l.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            list.HasMany(l => l.Items)
                .WithOne(i => i.GroceryList)
                .HasForeignKey(i => i.GroceryListId)
                .OnDelete(DeleteBehavior.Cascade);

            // Soft-deleted lists are hidden from every query unless IgnoreQueryFilters() is used.
            list.HasQueryFilter(l => l.DeletedAt == null);
        });

        builder.Entity<GroceryListItem>(item =>
        {
            item.Property(i => i.Name).IsRequired().HasMaxLength(100);
            item.Property(i => i.NormalizedName).IsRequired().HasMaxLength(100);
            item.Property(i => i.Quantity).HasPrecision(10, 2);
            item.Property(i => i.Unit).HasMaxLength(20);
            item.Property(i => i.Notes).HasMaxLength(500);

            item.HasIndex(i => new { i.GroceryListId, i.NormalizedName }).IsUnique();

            // Matches the GroceryList filter so items of a soft-deleted list are hidden too.
            item.HasQueryFilter(i => i.GroceryList!.DeletedAt == null);
        });
    }
}
