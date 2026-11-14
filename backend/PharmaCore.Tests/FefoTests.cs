using PharmaCore.Domain.Inventory;
using Xunit;

namespace PharmaCore.Tests;

public class FefoTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static BatchStock B(int day, decimal qty, int month = 12) => new(Guid.NewGuid(), new DateOnly(2027, month, day), qty);

    [Fact]
    public void Takes_earliest_expiry_first_and_spans_batches()
    {
        var late = B(1, 10, 12); var early = B(1, 4, 3);
        var r = FefoAllocator.Allocate([late, early], 6, Today);
        Assert.Equal(2, r.Count);
        Assert.Equal(early.BatchId, r[0].BatchId); Assert.Equal(4, r[0].Quantity);
        Assert.Equal(late.BatchId, r[1].BatchId); Assert.Equal(2, r[1].Quantity);
    }

    [Fact]
    public void Expired_batches_are_never_allocated()
    {
        var expired = new BatchStock(Guid.NewGuid(), new DateOnly(2026, 11, 1), 100);
        var ok = B(1, 5);
        var r = FefoAllocator.Allocate([expired, ok], 5, Today);
        Assert.Single(r); Assert.Equal(ok.BatchId, r[0].BatchId);
        Assert.Throws<InsufficientStockException>(() => FefoAllocator.Allocate([expired, ok], 6, Today));
    }

    [Fact]
    public void Batch_expiring_today_is_still_sellable()
        => Assert.Single(FefoAllocator.Allocate([new BatchStock(Guid.NewGuid(), Today, 3)], 3, Today));

    [Fact]
    public void Insufficient_stock_throws_with_available_amount()
    {
        var ex = Assert.Throws<InsufficientStockException>(() => FefoAllocator.Allocate([B(1, 2)], 5, Today));
        Assert.Equal(2, ex.Available);
    }

    [Theory] [InlineData(0)] [InlineData(-1)]
    public void Non_positive_quantity_is_rejected(decimal q)
        => Assert.Throws<ArgumentOutOfRangeException>(() => FefoAllocator.Allocate([B(1, 2)], q, Today));
}
