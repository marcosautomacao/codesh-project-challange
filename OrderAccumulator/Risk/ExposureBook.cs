namespace OrderAccumulator.Risk;

public sealed class ExposureBook
{
    private const decimal ExposureLimit = 100_000_000m;
    private static readonly HashSet<string> AllowedSymbols = ["PETR4", "VALE3", "VIIA4"];

    private readonly object _sync = new();
    private readonly Dictionary<string, decimal> _exposureBySymbol = new()
    {
        ["PETR4"] = 0m,
        ["VALE3"] = 0m,
        ["VIIA4"] = 0m,
    };

    public ExposureDecision Evaluate(string symbol, char side, decimal quantity, decimal price)
    {
        lock (_sync)
        {
            var currentExposure = _exposureBySymbol.GetValueOrDefault(symbol);

            if (!AllowedSymbols.Contains(symbol))
            {
                return Reject(currentExposure, "Unsupported symbol.");
            }

            if (side is not ('1' or '2'))
            {
                return Reject(currentExposure, "Unsupported side.");
            }

            if (quantity <= 0 || quantity >= 100_000 || decimal.Truncate(quantity) != quantity)
            {
                return Reject(currentExposure, "Quantity must be a positive integer below 100000.");
            }

            if (price <= 0 || price >= 1_000 || price * 100m != decimal.Truncate(price * 100m))
            {
                return Reject(currentExposure, "Price must be a positive cent amount below 1000.");
            }

            var signedOrderValue = price * quantity * (side == '1' ? 1m : -1m);
            var candidateExposure = currentExposure + signedOrderValue;
            if (candidateExposure > ExposureLimit || candidateExposure < -ExposureLimit)
            {
                return Reject(currentExposure, "Order would exceed the per-symbol exposure limit.");
            }

            _exposureBySymbol[symbol] = candidateExposure;
            return new ExposureDecision(true, candidateExposure, null);
        }
    }

    private static ExposureDecision Reject(decimal currentExposure, string reason) =>
        new(false, currentExposure, reason);
}
