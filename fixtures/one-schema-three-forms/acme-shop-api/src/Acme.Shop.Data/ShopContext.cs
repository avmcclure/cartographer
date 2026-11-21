using Microsoft.EntityFrameworkCore;

namespace Acme.Shop.Data;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public List<Order> Orders { get; } = [];
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime PlacedAt { get; set; }
    public string Status { get; set; } = "new";
    public List<OrderLine> Lines { get; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderAudit
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public DateTime ChangedAt { get; set; }
}

public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderAudit> OrderAudit => Set<OrderAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100);
            b.Property(c => c.Email).HasMaxLength(256);
        });
        modelBuilder.Entity<Product>(b =>
        {
            b.Property(p => p.Sku).HasMaxLength(20).IsUnicode(false);
            b.Property(p => p.Name).HasMaxLength(100);
            b.Property(p => p.Price).HasColumnType("decimal(10,2)");
        });
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable(t => t.HasTrigger("trg_Orders_Audit"));
            b.Property(o => o.PlacedAt).HasColumnType("datetime2(0)");
            b.Property(o => o.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("new");
        });
        modelBuilder.Entity<OrderLine>(b => b.Property(l => l.UnitPrice).HasColumnType("decimal(10,2)"));
        modelBuilder.Entity<OrderAudit>(b =>
        {
            b.ToTable("OrderAudit");
            b.Property(a => a.Status).HasMaxLength(20).IsUnicode(false);
            b.Property(a => a.ChangedAt).HasColumnType("datetime2(0)");
        });
    }
}
