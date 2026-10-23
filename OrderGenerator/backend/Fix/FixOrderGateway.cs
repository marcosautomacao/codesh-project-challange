using System.Collections.Concurrent;
using OrderGenerator.Orders;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace OrderGenerator.Fix;

public sealed class FixOrderGateway
{
    private static readonly SessionID SessionId = new("FIX.4.4", "OrderGenerator", "OrderAccumulator");

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingOrders = new(StringComparer.Ordinal);
    private readonly SocketInitiator _initiator;

    public FixOrderGateway(string configurationPath)
    {
        var settings = new SessionSettings(configurationPath);
        var application = new FixOrderSessionApplication(_pendingOrders);
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new FileLogFactory(settings);

        _initiator = new SocketInitiator(application, storeFactory, settings, logFactory);
    }

    public void Start() => _initiator.Start();

    public void Stop() => _initiator.Stop();

    public async Task<string?> SubmitAsync(OrderRequest request)
    {
        var clOrdId = Guid.NewGuid().ToString("N");
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingOrders[clOrdId] = response;

        try
        {
            var order = new QuickFix.FIX44.NewOrderSingle();
            order.SetField(new ClOrdID(clOrdId));
            order.SetField(new HandlInst('1'));
            order.SetField(new Symbol(request.Symbol));
            order.SetField(new Side(request.Side == "Compra" ? '1' : '2'));
            order.SetField(new TransactTime(DateTime.UtcNow));
            order.SetField(new OrdType('2'));
            order.SetField(new OrderQty(request.Quantity));
            order.SetField(new Price(request.Price));

            if (!Session.SendToTarget(order, SessionId))
            {
                return null;
            }

            return await response.Task;
        }
        finally
        {
            _pendingOrders.TryRemove(clOrdId, out _);
        }
    }
}
