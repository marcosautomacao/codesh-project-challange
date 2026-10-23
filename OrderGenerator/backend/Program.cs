using OrderGenerator.Fix;
using OrderGenerator.Orders;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapGet("/", () => "OrderGenerator");

var fixGateway = new FixOrderGateway(Path.Combine(AppContext.BaseDirectory, "quickfix.cfg"));
fixGateway.Start();
app.Lifetime.ApplicationStopping.Register(fixGateway.Stop);

app.MapOrderEndpoints(fixGateway);
app.Run();
