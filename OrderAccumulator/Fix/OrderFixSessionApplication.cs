using OrderAccumulator.Risk;
using QuickFix;
using QuickFix.Fields;

namespace OrderAccumulator.Fix;

internal sealed class OrderFixSessionApplication(ExposureBook exposureBook) : IApplication
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
        var quantity = message.GetDecimal(Tags.OrderQty);
        var price = message.GetDecimal(Tags.Price);
        var decision = exposureBook.Evaluate(symbol, side, quantity, price);
        var execType = decision.Accepted ? '0' : '8';

        var report = new QuickFix.FIX44.ExecutionReport();
        report.SetField(new OrderID(clOrdId));
        report.SetField(new ExecID(Guid.NewGuid().ToString("N")));
        report.SetField(new ExecType(execType));
        report.SetField(new OrdStatus(execType));
        report.SetField(new ClOrdID(clOrdId));
        report.SetField(new Symbol(symbol));
        report.SetField(new Side(side));
        report.SetField(new LeavesQty(decision.Accepted ? quantity : 0m));
        report.SetField(new CumQty(0));
        report.SetField(new AvgPx(0));

        if (decision.RejectionReason is not null)
        {
            report.SetField(new Text(decision.RejectionReason));
        }

        Session.SendToTarget(report, sessionID);

        Console.WriteLine(
            $"FIX order {clOrdId} {symbol}: {(decision.Accepted ? "New" : "Rejected")}; exposure {decision.ResultingExposure:N2}");
    }
}
