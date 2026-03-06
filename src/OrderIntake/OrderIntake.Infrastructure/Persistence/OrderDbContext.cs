using Microsoft.EntityFrameworkCore;
using OrderIntake.Domain.Entities;
using OrderIntake.Domain.Enums;
using OrderIntake.Domain.ValueObjects;

namespace OrderIntake.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the Order Intake service.
/// </summary>
public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>Gets the Orders table.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).HasColumnName("id");
            entity.Property(o => o.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(o => o.CustomerEmail).HasColumnName("customer_email").HasMaxLength(255).IsRequired();
            entity.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.Property(o => o.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .HasConversion(s => s.ToString(), s => Enum.Parse<OrderStatus>(s))
                .IsRequired();

            // Money as owned entity (total_amount, currency columns)
            entity.OwnsOne(o => o.Total, money =>
            {
                money.Property(m => m.Amount).HasColumnName("total_amount").HasColumnType("decimal(18,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            });

            // Address as owned entity (shipping_* columns)
            entity.OwnsOne(o => o.ShippingAddress, addr =>
            {
                addr.Property(a => a.Street).HasColumnName("shipping_street").HasMaxLength(255).IsRequired();
                addr.Property(a => a.City).HasColumnName("shipping_city").HasMaxLength(100).IsRequired();
                addr.Property(a => a.Country).HasColumnName("shipping_country").HasMaxLength(2).IsRequired();
                addr.Property(a => a.ZipCode).HasColumnName("shipping_zip_code").HasMaxLength(20).IsRequired();
            });

            // OrderLines as a separate table
            entity.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("order_lines");
                line.Property<Guid>("Id").HasColumnName("id");
                line.HasKey("Id");
                line.Property(l => l.ProductId).HasColumnName("product_id").IsRequired();
                line.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(255).IsRequired();
                line.Property(l => l.Quantity).HasColumnName("quantity").IsRequired();
                line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(18,2)").IsRequired();
                line.Property(l => l.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            });
        });
    }
}
