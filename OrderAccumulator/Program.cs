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

        var report = new QuickFix.FIX44.ExecutionReport();
        report.SetField(new OrderID(clOrdId));
        report.SetField(new ExecID(Guid.NewGuid().ToString("N")));
        report.SetField(new ExecType('0'));
        report.SetField(new OrdStatus('0'));
        report.SetField(new ClOrdID(clOrdId));
        report.SetField(new Symbol(symbol));
        report.SetField(new Side(side));
        report.SetField(new LeavesQty(orderQty));
        report.SetField(new CumQty(0));
        report.SetField(new AvgPx(0));

        Session.SendToTarget(report, sessionID);
    }
}
