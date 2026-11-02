using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", table => table.HasCheckConstraint("CK_Roles_AdminIdentity",
            "([RoleId] = 1 AND [RoleName] = 'Admin') OR ([RoleId] <> 1 AND [RoleName] <> 'Admin')"));
        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleName).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.RoleName).IsUnique();
        builder.HasData(new Role { RoleId = SystemRoles.AdminId, RoleName = SystemRoles.Admin },
            new Role { RoleId = 2, RoleName = SystemRoles.Customer });
    }
}

