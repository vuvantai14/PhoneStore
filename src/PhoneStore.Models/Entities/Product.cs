namespace PhoneStore.Models.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public int CategoryId { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? RAM { get; set; }
    public string? Storage { get; set; }
    public string? Chip { get; set; }
    public string? Screen { get; set; }
    public string? Camera { get; set; }
    public string? Battery { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Brand Brand { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

