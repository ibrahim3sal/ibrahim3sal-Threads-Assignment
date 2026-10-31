using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly ProductRepository _productRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly CouponRepository _couponRepository;
    private readonly HashSet<int> _failShippingForOrderIds;
    public PricingService(
        ProductRepository productRepository,
        CustomerRepository customerRepository,
        CouponRepository couponRepository,
        IEnumerable<int>? failShippingForOrderIds = null,
        bool enableFailures = true)
    {
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;

        _failShippingForOrderIds = enableFailures
            ? failShippingForOrderIds?.ToHashSet() ?? new HashSet<int>()
            : new HashSet<int>();
    }

    public async Task<decimal> CalculateSubTotalAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        Log.Info($"Calculating subtotal for Order #{order.Id}");

        Log.Info("PricingService -> Opening database connection...");
        await Task.Delay(200, cancellationToken);

        Log.Info("PricingService -> Executing product query...");
        await Task.Delay(
            Random.Shared.Next(300, 601),
            cancellationToken);

        decimal subTotal = 0;

        foreach (OrderItem item in order.Items)
        {
            Product? product = await _productRepository.GetByIdAsync(
                item.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found.");
            }

            item.UnitPrice = product.Price;

            subTotal += product.Price * item.Quantity;
        }

        subTotal = Math.Round(
            subTotal,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"Order #{order.Id} SubTotal = {subTotal:F2}");

        return subTotal;
    }

    public async Task<decimal> GetDiscountAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        Log.Info($"Calculating discount for Order #{order.Id}");

        Log.Info("Discount API -> DNS and handshake...");
        await Task.Delay(150, cancellationToken);

        Log.Info("Discount API -> Sending request...");
        await Task.Delay(300, cancellationToken);

        Log.Info("Discount API -> Waiting for response...");
        await Task.Delay(500, cancellationToken);

        Customer? customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            cancellationToken);

        if (customer is null)
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found.");

        decimal loyaltyDiscount = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0,
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        decimal couponDiscount = 0;

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode,
                cancellationToken);

            if (coupon is null)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate < DateTime.Today)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");
            }

            couponDiscount = coupon.DiscountPercent;
        }

        decimal discountPercentage =
            Math.Min(loyaltyDiscount + couponDiscount, 30);

        decimal discount = order.SubTotal * discountPercentage / 100;

        discount = Math.Round(
            discount,
            2,
            MidpointRounding.AwayFromZero);

        order.Discount = discount;

        Log.Success(
            $"Order #{order.Id} Discount = {discount:F2} ({discountPercentage}%)");

        return discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(
    Order order,
    CancellationToken cancellationToken = default)
    {
        Log.Info($"Calculating shipping for Order #{order.Id}");

        Log.Info("Shipping API -> DNS and handshake...");
        await Task.Delay(150, cancellationToken);

        Log.Info("Shipping API -> Sending request...");
        await Task.Delay(300, cancellationToken);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException(
                "Shipping provider unavailable");
        }

        Log.Info("Shipping API -> Waiting for response...");
        await Task.Delay(
            Random.Shared.Next(500, 1501),
            cancellationToken);

        decimal shippingCost = 0;

        if (order.SubTotal >= 1000)
        {
            order.ShippingCost = 0;
            return 0;
        }

        int totalQuantity = 0;
        decimal totalWeight = 0;

        foreach (OrderItem item in order.Items)
        {
            Product? product = await _productRepository.GetByIdAsync(
                item.ProductId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found.");
            }

            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        shippingCost = 5m + (1.5m * totalQuantity) + (2m * totalWeight);

        shippingCost = Math.Round(
            shippingCost,
            2,
            MidpointRounding.AwayFromZero);

        order.ShippingCost = shippingCost;

        Log.Success(
            $"Order #{order.Id} Shipping Cost = {shippingCost:F2}");

        return shippingCost;
    }

    public async Task<decimal> CalculateTotalAsync(
    Order order,
    CancellationToken cancellationToken = default)
    {
        order.SubTotal = await CalculateSubTotalAsync(
            order,
            cancellationToken);

        Task<decimal> discountTask = GetDiscountAsync(
            order,
            cancellationToken);

        Task<decimal> shippingTask = CalculateShippingCostAsync(
            order,
            cancellationToken);

        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        return order.TotalAmount;
    }
}