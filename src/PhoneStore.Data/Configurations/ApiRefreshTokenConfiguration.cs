using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public sealed class ApiRefreshTokenConfiguration : IEntityTypeConfiguration<ApiRefreshToken>
{
    public void Configure(EntityTypeBuilder<ApiRefreshToken> builder)
    {
        builder.ToTable("ApiRefreshTokens");
        builder.HasKey(x => x.ApiRefreshTokenId);
        builder.Property(x => x.FamilyId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(64);
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);

        builder.HasIndex(x => x.TokenHash, "UX_ApiRefreshTokens_TokenHash").IsUnique();
        builder.HasIndex(x => x.UserId, "IX_ApiRefreshTokens_UserId");
        builder.HasIndex(x => x.FamilyId, "IX_ApiRefreshTokens_FamilyId");

        builder.HasOne(x => x.User)
            .WithMany(u => u.ApiRefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
