using System.Collections.Concurrent;
using QuickFix;
using QuickFix.Fields;

namespace OrderGenerator.Fix;

internal sealed class FixOrderSessionApplication(
    ConcurrentDictionary<string, TaskCompletionSource<string>> pendingOrders) : IApplication
{
    public void OnCreate(SessionID sessionID) { }

    public void OnLogon(SessionID sessionID) => Console.WriteLine($"FIX logon: {sessionID}");

    public void OnLogout(SessionID sessionID) => Console.WriteLine($"FIX logout: {sessionID}");

    public void ToAdmin(Message message, SessionID sessionID) { }

    public void ToApp(Message message, SessionID sessionID) { }

    public void FromAdmin(Message message, SessionID sessionID) { }

    public void FromApp(Message message, SessionID sessionID)
    {
        if (message.Header.GetString(Tags.MsgType) != "8" ||
            !message.IsSetField(Tags.ClOrdID) ||
            !message.IsSetField(Tags.ExecType))
        {
            return;
        }

        var clOrdId = message.GetString(Tags.ClOrdID);
        var execType = message.GetChar(Tags.ExecType) switch
        {
            '0' => "New",
            '8' => "Rejected",
            var value => value.ToString(),
        };

        if (pendingOrders.TryRemove(clOrdId, out var response))
        {
            response.TrySetResult(execType);
        }
    }
}
