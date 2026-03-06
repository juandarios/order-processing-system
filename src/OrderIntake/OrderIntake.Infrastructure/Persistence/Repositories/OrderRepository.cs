using Microsoft.EntityFrameworkCore;
using OrderIntake.Application.Interfaces;
using OrderIntake.Domain.Entities;

namespace OrderIntake.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IOrderRepository"/>.
/// </summary>
public class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Orders
            .Include("Lines")
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await dbContext.Orders.AddAsync(order, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        dbContext.Orders.Update(order);
        await dbContext.SaveChangesAsync(ct);
    }
}
