namespace OrderGenerator.Orders;

public sealed record OrderRequest(string Symbol, string Side, int Quantity, decimal Price);
