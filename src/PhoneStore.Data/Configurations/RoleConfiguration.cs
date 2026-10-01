using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleName).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.RoleName).IsUnique();
        builder.HasData(new Role { RoleId = 1, RoleName = "Admin" }, new Role { RoleId = 2, RoleName = "Customer" });
    }
}

