using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase4.Shipping;

public interface IShippingCarrier
{
    string Name { get; }

    Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct);
}

public class FastShipCarrier : IShippingCarrier
{
    private readonly HashSet<int> _failForOrderIds;

    public string Name => "FastShip";

    public FastShipCarrier(IEnumerable<int>? failForOrderIds = null)
    {
        _failForOrderIds =
            failForOrderIds?.ToHashSet() ?? new HashSet<int>();
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);

            if (_failForOrderIds.Contains(order.Id))
            {
                throw new ShippingProviderUnavailableException(
                    $"{Name} unavailable");
            }

            await Task.Delay(
                Random.Shared.Next(500, 1501), ct);

            return Math.Round(
                order.ShippingCost * 1.00m,
                2,
                MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{Name}] cancelled");
            throw;
        }
    }
}

public class CairoExpressCarrier : IShippingCarrier
{
    private readonly HashSet<int> _failForOrderIds;

    public string Name => "CairoExpress";

    public CairoExpressCarrier(IEnumerable<int>? failForOrderIds = null)
    {
        _failForOrderIds =
            failForOrderIds?.ToHashSet() ?? new HashSet<int>();
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);

            if (order.Id % 2 == 0 ||
                _failForOrderIds.Contains(order.Id))
            {
                throw new ShippingProviderUnavailableException(
                    $"{Name} unavailable");
            }

            await Task.Delay(
                Random.Shared.Next(300, 2001), ct);

            return Math.Round(
                order.ShippingCost * 0.95m,
                2,
                MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{Name}] cancelled");
            throw;
        }
    }
}

public class NileCourierCarrier : IShippingCarrier
{
    private readonly HashSet<int> _failForOrderIds;

    public string Name => "NileCourier";

    public NileCourierCarrier(IEnumerable<int>? failForOrderIds = null)
    {
        _failForOrderIds =
            failForOrderIds?.ToHashSet() ?? new HashSet<int>();
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);

            if (_failForOrderIds.Contains(order.Id))
            {
                throw new ShippingProviderUnavailableException(
                    $"{Name} unavailable");
            }

            await Task.Delay(
                Random.Shared.Next(800, 1201), ct);

            return Math.Round(
                order.ShippingCost * 1.10m,
                2,
                MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[{Name}] cancelled");
            throw;
        }
    }
}