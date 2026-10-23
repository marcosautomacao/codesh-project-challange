using OrderGenerator.Fix;

namespace OrderGenerator.Orders;

public static class OrderEndpointMapping
{
    public static void MapOrderEndpoints(this WebApplication app, FixOrderGateway fixGateway)
    {
        app.MapPost("/orders", (OrderRequest request) => SubmitOrderAsync(request, fixGateway));
    }

    private static async Task<IResult> SubmitOrderAsync(OrderRequest request, FixOrderGateway fixGateway)
    {
        if (!OrderRequestValidator.IsValid(request))
        {
            return Results.BadRequest();
        }

        var execType = await fixGateway.SubmitAsync(request);
        return execType is null
            ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new OrderResponse(execType));
    }
}
