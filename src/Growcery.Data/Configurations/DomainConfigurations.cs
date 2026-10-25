using Growcery.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Growcery.Data.Configurations;

// Ids are created by the domain (Guid.CreateVersion7), so EF must never generate them. This also makes EF treat
// a new item added to a tracked list as an insert rather than an update.

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> person)
    {
        person.ToTable("People");
        person.HasKey(p => p.Id);
        person.Property(p => p.Id).ValueGeneratedNever();
        person.Property(p => p.ExternalId).IsRequired().HasMaxLength(256);
        person.Property(p => p.DisplayName).IsRequired().HasMaxLength(256);

        // Links to the identity provider's user id. Deliberately no foreign key to AspNetUsers (spec 002 D15).
        person.HasIndex(p => p.ExternalId).IsUnique();
    }
}

public sealed class GroceryListConfiguration : IEntityTypeConfiguration<GroceryList>
{
    public void Configure(EntityTypeBuilder<GroceryList> list)
    {
        list.ToTable("GroceryLists");
        list.HasKey(l => l.Id);
        list.Property(l => l.Id).ValueGeneratedNever();
        list.Property(l => l.Name).IsRequired().HasMaxLength(ListName.MaxLength);

        // The app hard-deletes a list together with its members and items (spec 002 D12), so the database cascades the same way.
        list.HasMany(l => l.Members)
            .WithOne()
            .HasForeignKey(m => m.GroceryListId)
            .OnDelete(DeleteBehavior.Cascade);
        list.Navigation(l => l.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        list.HasMany(l => l.Items)
            .WithOne()
            .HasForeignKey(i => i.GroceryListId)
            .OnDelete(DeleteBehavior.Cascade);
        list.Navigation(l => l.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ListMemberConfiguration : IEntityTypeConfiguration<ListMember>
{
    public void Configure(EntityTypeBuilder<ListMember> member)
    {
        member.ToTable("ListMembers");
        member.HasKey(m => new { m.GroceryListId, m.PersonId });
        member.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        // The app never deletes a Person, so the database must not silently remove their memberships either.
        member.HasOne<Person>()
            .WithMany()
            .HasForeignKey(m => m.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        member.HasIndex(m => m.PersonId);
    }
}

public sealed class GroceryListItemConfiguration : IEntityTypeConfiguration<GroceryListItem>
{
    public void Configure(EntityTypeBuilder<GroceryListItem> item)
    {
        item.ToTable("GroceryListItems");
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).ValueGeneratedNever();
        item.Property(i => i.Name).IsRequired().HasMaxLength(ItemDetails.MaxNameLength);
        item.Property(i => i.Quantity).HasPrecision(10, ItemDetails.QuantityDecimalPlaces);

        // Stored by name so new units can be added without a data migration.
        item.Property(i => i.Unit).HasConversion<string>().HasMaxLength(20);
    }
}
