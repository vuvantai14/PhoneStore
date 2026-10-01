namespace PhoneStore.Models.Entities;

public class Brand
{
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

