using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages", table =>
        {
            table.HasCheckConstraint("CK_ProductImages_DisplayOrder", "[DisplayOrder] >= 0");
        });
        builder.HasKey(x => x.ImageId);
        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.IsPrimary).HasDefaultValue(false);
        builder.HasOne(x => x.Product).WithMany(x => x.ProductImages).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

