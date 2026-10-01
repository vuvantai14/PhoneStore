using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("OrderDetails", table =>
        {
            table.HasCheckConstraint("CK_OrderDetails_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_OrderDetails_UnitPrice", "[UnitPrice] >= 0");
        });
        builder.HasKey(x => x.OrderDetailId);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.HasOne(x => x.Order).WithMany(x => x.OrderDetails).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany(x => x.OrderDetails).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }
}

