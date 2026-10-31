using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(PricingService pricingService)
    {
        _pricingService = pricingService;
    }

    public async Task PriceOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        order.SubTotal = await _pricingService.CalculateSubTotalAsync(
            order,
            cancellationToken);

        Task<decimal> discountTask =
            _pricingService.GetDiscountAsync(order, cancellationToken);

        Task<decimal> shippingTask =
            _pricingService.CalculateShippingCostAsync(
                order,
                cancellationToken);

        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);
    }

    public async Task PriceOrderSequentialAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        order.SubTotal = await _pricingService.CalculateSubTotalAsync(
            order,
            cancellationToken);

        order.Discount = await _pricingService.GetDiscountAsync(
            order,
            cancellationToken);

        order.ShippingCost =
            await _pricingService.CalculateShippingCostAsync(
                order,
                cancellationToken);

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);
    }
}