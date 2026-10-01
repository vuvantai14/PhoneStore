using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_TotalAmount", "[TotalAmount] >= 0");
        });
        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ReceiverName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ReceiverPhone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ShippingAddress).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.OrderDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.OrderCode).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(PhoneStore.Models.Enums.OrderStatus.Pending);
        builder.HasOne(x => x.User).WithMany(x => x.Orders).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

