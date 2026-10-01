using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "[Price] > 0");
            table.HasCheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0");
        });
        builder.HasKey(x => x.ProductId);
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.RAM).HasMaxLength(50);
        builder.Property(x => x.Storage).HasMaxLength(50);
        builder.Property(x => x.Chip).HasMaxLength(150);
        builder.Property(x => x.Screen).HasMaxLength(200);
        builder.Property(x => x.Camera).HasMaxLength(250);
        builder.Property(x => x.Battery).HasMaxLength(100);
        builder.Property(x => x.OperatingSystem).HasMaxLength(100);
        builder.Property(x => x.Description);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(x => x.Brand).WithMany(x => x.Products).HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}

