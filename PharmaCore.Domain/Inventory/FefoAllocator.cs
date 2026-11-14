namespace PharmaCore.Domain.Inventory;

public sealed record BatchStock(Guid BatchId, DateOnly ExpiryDate, decimal Quantity);
public sealed record Allocation(Guid BatchId, decimal Quantity);

public sealed class InsufficientStockException(decimal requested, decimal available)
    : Exception($"Requested {requested} but only {available} unexpired units are available.")
{
    public decimal Requested { get; } = requested;
    public decimal Available { get; } = available;
}

/// <summary>First-Expired-First-Out allocation. Pure logic: no I/O, so it is unit-testable.</summary>
public static class FefoAllocator
{
    /// <param name="today">Batches with ExpiryDate earlier than today are never allocated.</param>
    public static IReadOnlyList<Allocation> Allocate(IEnumerable<BatchStock> stock, decimal required, DateOnly today)
    {
        if (required <= 0) throw new ArgumentOutOfRangeException(nameof(required), "Quantity must be positive.");

        var usable = stock.Where(s => s.Quantity > 0 && s.ExpiryDate >= today)
                          .OrderBy(s => s.ExpiryDate).ThenBy(s => s.BatchId).ToList();
        var available = usable.Sum(s => s.Quantity);
        if (available < required) throw new InsufficientStockException(required, available);

        var result = new List<Allocation>();
        var remaining = required;
        foreach (var b in usable)
        {
            if (remaining == 0) break;
            var take = Math.Min(b.Quantity, remaining);
            result.Add(new Allocation(b.BatchId, take));
            remaining -= take;
        }
        return result;
    }
}
