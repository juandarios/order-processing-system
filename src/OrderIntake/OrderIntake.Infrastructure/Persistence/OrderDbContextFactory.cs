using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderIntake.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating <see cref="OrderDbContext"/> instances.
/// Used by EF Core CLI tools (dotnet ef migrations add).
/// </summary>
public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    /// <inheritdoc />
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=order-intake-db;Username=postgres;Password=postgres");

        return new OrderDbContext(optionsBuilder.Options);
    }
}
