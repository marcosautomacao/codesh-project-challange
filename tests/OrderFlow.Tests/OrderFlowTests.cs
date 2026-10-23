using OrderAccumulator.Risk;
using OrderGenerator.Orders;
using Xunit;

namespace OrderFlow.Tests;

public sealed class OrderRequestValidatorTests
{
    [Theory]
    [InlineData("PETR4", "Compra")]
    [InlineData("PETR4", "Venda")]
    [InlineData("VALE3", "Compra")]
    [InlineData("VALE3", "Venda")]
    [InlineData("VIIA4", "Compra")]
    [InlineData("VIIA4", "Venda")]
    public void AcceptsEverySupportedSymbolAndSide(string symbol, string side)
    {
        Assert.True(OrderRequestValidator.IsValid(new OrderRequest(symbol, side, 1, 0.01m)));
    }

    [Fact]
    public void AcceptsMinimumAndMaximumOrderValues()
    {
        Assert.True(OrderRequestValidator.IsValid(new OrderRequest("PETR4", "Compra", 1, 0.01m)));
        Assert.True(OrderRequestValidator.IsValid(new OrderRequest("VIIA4", "Venda", 99_999, 999.99m)));
    }

    [Fact]
    public void RejectsUnsupportedAndOutOfRangeValues()
    {
        var invalidOrders = new[]
        {
            new OrderRequest("INVALID", "Compra", 1, 1m),
            new OrderRequest("PETR4", "Hold", 1, 1m),
            new OrderRequest("PETR4", "Compra", 0, 1m),
            new OrderRequest("PETR4", "Compra", 100_000, 1m),
            new OrderRequest("PETR4", "Compra", 1, 0m),
            new OrderRequest("PETR4", "Compra", 1, -0.01m),
            new OrderRequest("PETR4", "Compra", 1, 0.001m),
            new OrderRequest("PETR4", "Compra", 1, 1_000m),
        };

        Assert.All(invalidOrders, order => Assert.False(OrderRequestValidator.IsValid(order)));
    }
}

public sealed class ExposureBookTests
{
    [Fact]
    public void BuysIncreaseAndSellsDecreaseExposureIncludingAcrossZero()
    {
        var book = new ExposureBook();

        var buy = book.Evaluate("PETR4", '1', 10m, 5.25m);
        var sell = book.Evaluate("PETR4", '2', 4m, 5.25m);
        var sellAcrossZero = book.Evaluate("PETR4", '2', 10m, 5.25m);

        Assert.True(buy.Accepted);
        Assert.Equal(52.50m, buy.ResultingExposure);
        Assert.True(sell.Accepted);
        Assert.Equal(31.50m, sell.ResultingExposure);
        Assert.True(sellAcrossZero.Accepted);
        Assert.Equal(-21.00m, sellAcrossZero.ResultingExposure);
    }

    [Fact]
    public void AcceptsExactPositiveLimitAndRejectsAnOrderBeyondItWithoutChangingExposure()
    {
        var book = new ExposureBook();

        Assert.Equal(99_998_000.01m, book.Evaluate("PETR4", '1', 99_999m, 999.99m).ResultingExposure);
        Assert.Equal(99_999_999.99m, book.Evaluate("PETR4", '1', 99_999m, 0.02m).ResultingExposure);
        var atLimit = book.Evaluate("PETR4", '1', 1m, 0.01m);
        var beyondLimit = book.Evaluate("PETR4", '1', 1m, 0.01m);

        Assert.True(atLimit.Accepted);
        Assert.Equal(100_000_000m, atLimit.ResultingExposure);
        Assert.False(beyondLimit.Accepted);
        Assert.Equal(100_000_000m, beyondLimit.ResultingExposure);

        var reducedExposure = book.Evaluate("PETR4", '2', 1m, 0.01m);
        Assert.True(reducedExposure.Accepted);
        Assert.Equal(99_999_999.99m, reducedExposure.ResultingExposure);
    }

    [Fact]
    public void AcceptsExactNegativeLimitAndRejectsAnOrderBeyondIt()
    {
        var book = new ExposureBook();

        Assert.Equal(-99_998_000.01m, book.Evaluate("VALE3", '2', 99_999m, 999.99m).ResultingExposure);
        Assert.Equal(-99_999_999.99m, book.Evaluate("VALE3", '2', 99_999m, 0.02m).ResultingExposure);
        var atLimit = book.Evaluate("VALE3", '2', 1m, 0.01m);
        var beyondLimit = book.Evaluate("VALE3", '2', 1m, 0.01m);

        Assert.True(atLimit.Accepted);
        Assert.Equal(-100_000_000m, atLimit.ResultingExposure);
        Assert.False(beyondLimit.Accepted);
        Assert.Equal(-100_000_000m, beyondLimit.ResultingExposure);
    }

    [Fact]
    public void ExposureIsIndependentForEachAsset()
    {
        var book = new ExposureBook();
        book.Evaluate("PETR4", '1', 99_999m, 999.99m);
        book.Evaluate("PETR4", '1', 99_999m, 0.02m);
        book.Evaluate("PETR4", '1', 1m, 0.01m);

        var otherSymbol = book.Evaluate("VALE3", '1', 1m, 0.01m);

        Assert.True(otherSymbol.Accepted);
        Assert.Equal(0.01m, otherSymbol.ResultingExposure);
    }

    [Fact]
    public void RejectedInvalidOrderDoesNotChangeExposure()
    {
        var book = new ExposureBook();
        var accepted = book.Evaluate("VIIA4", '1', 10m, 5m);

        var rejected = book.Evaluate("VIIA4", '1', 0m, 5m);

        Assert.True(accepted.Accepted);
        Assert.False(rejected.Accepted);
        Assert.Equal(50m, rejected.ResultingExposure);
        Assert.Equal("Quantity must be a positive integer below 100000.", rejected.RejectionReason);
    }
}
