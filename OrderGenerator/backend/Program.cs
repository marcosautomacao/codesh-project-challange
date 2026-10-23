using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;
using System.Collections.Concurrent;
using QuickFix.Fields;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => "OrderGenerator");

var settings = new SessionSettings(Path.Combine(AppContext.BaseDirectory, "quickfix.cfg"));
var pendingOrders = new ConcurrentDictionary<string, TaskCompletionSource<string>>(StringComparer.Ordinal);
var fixApplication = new FixSessionApplication(pendingOrders);
var storeFactory = new FileStoreFactory(settings);
var logFactory = new FileLogFactory(settings);
var initiator = new SocketInitiator(fixApplication, storeFactory, settings, logFactory);

initiator.Start();
app.Lifetime.ApplicationStopping.Register(initiator.Stop);

app.MapPost("/orders", SubmitOrderAsync);

app.Run();

async Task<IResult> SubmitOrderAsync(OrderRequest request)
{
    if (!IsValidOrder(request))
    {
        return Results.BadRequest();
    }

    var clOrdId = Guid.NewGuid().ToString("N");
    var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    pendingOrders[clOrdId] = response;

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

        var sessionId = new SessionID("FIX.4.4", "OrderGenerator", "OrderAccumulator");
        if (!Session.SendToTarget(order, sessionId))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var execType = await response.Task;
        return Results.Ok(new { execType });
    }
    finally
    {
        pendingOrders.TryRemove(clOrdId, out _);
    }
}

static bool IsValidOrder(OrderRequest request)
{
    return request.Symbol is "PETR4" or "VALE3" or "VIIA4" &&
           request.Side is "Compra" or "Venda" &&
           request.Quantity > 0 && request.Quantity < 100_000 &&
           request.Price > 0 && request.Price < 1_000 &&
           request.Price % 0.01m == 0;
}

sealed record OrderRequest(string Symbol, string Side, int Quantity, decimal Price);

sealed class FixSessionApplication(
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
