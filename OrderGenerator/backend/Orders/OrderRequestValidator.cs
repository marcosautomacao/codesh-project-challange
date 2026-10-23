namespace OrderGenerator.Orders;

public static class OrderRequestValidator
{
    public static bool IsValid(OrderRequest request)
    {
        return request.Symbol is "PETR4" or "VALE3" or "VIIA4" &&
               request.Side is "Compra" or "Venda" &&
               request.Quantity > 0 && request.Quantity < 100_000 &&
               request.Price > 0 && request.Price < 1_000 &&
               request.Price % 0.01m == 0;
    }
}
