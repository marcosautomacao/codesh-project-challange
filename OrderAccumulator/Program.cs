using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;

var settings = new SessionSettings(Path.Combine(AppContext.BaseDirectory, "quickfix.cfg"));
var fixApplication = new FixSessionApplication();
var storeFactory = new FileStoreFactory(settings);
var logFactory = new FileLogFactory(settings);
var acceptor = new ThreadedSocketAcceptor(fixApplication, storeFactory, settings, logFactory);
using var shutdown = new ManualResetEventSlim(false);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Set();
};

acceptor.Start();
Console.WriteLine("OrderAccumulator FIX acceptor started. Press Ctrl+C to stop.");
shutdown.Wait();
acceptor.Stop();

sealed class FixSessionApplication : IApplication
{
    private const decimal ExposureLimit = 100_000_000m;
    private static readonly HashSet<string> AllowedSymbols = ["PETR4", "VALE3", "VIIA4"];

    private readonly object _exposureLock = new();
    private readonly Dictionary<string, decimal> _exposureBySymbol = new()
    {
        ["PETR4"] = 0m,
        ["VALE3"] = 0m,
        ["VIIA4"] = 0m,
    };

    public void OnCreate(SessionID sessionID) { }

    public void OnLogon(SessionID sessionID) => Console.WriteLine($"FIX logon: {sessionID}");

    public void OnLogout(SessionID sessionID) => Console.WriteLine($"FIX logout: {sessionID}");

    public void ToAdmin(Message message, SessionID sessionID) { }

    public void ToApp(Message message, SessionID sessionID) { }

    public void FromAdmin(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID)
    {
        if (message.Header.GetString(Tags.MsgType) != "D")
        {
            return;
        }

        var clOrdId = message.GetString(Tags.ClOrdID);
        var symbol = message.GetString(Tags.Symbol);
        var side = message.GetChar(Tags.Side);
        var orderQty = message.GetDecimal(Tags.OrderQty);
        var price = message.GetDecimal(Tags.Price);
        var accepted = TryApplyOrder(symbol, side, orderQty, price, out var resultingExposure, out var rejectionReason);
        var execType = accepted ? '0' : '8';

        var report = new QuickFix.FIX44.ExecutionReport();
        report.SetField(new OrderID(clOrdId));
        report.SetField(new ExecID(Guid.NewGuid().ToString("N")));
        report.SetField(new ExecType(execType));
        report.SetField(new OrdStatus(execType));
        report.SetField(new ClOrdID(clOrdId));
        report.SetField(new Symbol(symbol));
        report.SetField(new Side(side));
        report.SetField(new LeavesQty(accepted ? orderQty : 0m));
        report.SetField(new CumQty(0));
        report.SetField(new AvgPx(0));

        if (!accepted)
        {
            report.SetField(new Text(rejectionReason));
        }

        Session.SendToTarget(report, sessionID);

        Console.WriteLine(
            $"FIX order {clOrdId} {symbol}: {(accepted ? "New" : "Rejected")}; exposure {resultingExposure:N2}");
    }

    private bool TryApplyOrder(
        string symbol,
        char side,
        decimal quantity,
        decimal price,
        out decimal resultingExposure,
        out string rejectionReason)
    {
        lock (_exposureLock)
        {
            resultingExposure = _exposureBySymbol.GetValueOrDefault(symbol);

            if (!AllowedSymbols.Contains(symbol))
            {
                rejectionReason = "Unsupported symbol.";
                return false;
            }

            if (side is not ('1' or '2'))
            {
                rejectionReason = "Unsupported side.";
                return false;
            }

            if (quantity <= 0 || quantity >= 100_000 || decimal.Truncate(quantity) != quantity)
            {
                rejectionReason = "Quantity must be a positive integer below 100000.";
                return false;
            }

            if (price <= 0 || price >= 1_000 || price * 100m != decimal.Truncate(price * 100m))
            {
                rejectionReason = "Price must be a positive cent amount below 1000.";
                return false;
            }

            var signedOrderValue = price * quantity * (side == '1' ? 1m : -1m);
            var candidateExposure = resultingExposure + signedOrderValue;
            if (candidateExposure > ExposureLimit || candidateExposure < -ExposureLimit)
            {
                rejectionReason = "Order would exceed the per-symbol exposure limit.";
                return false;
            }

            _exposureBySymbol[symbol] = candidateExposure;
            resultingExposure = candidateExposure;
            rejectionReason = string.Empty;
            return true;
        }
    }
}
