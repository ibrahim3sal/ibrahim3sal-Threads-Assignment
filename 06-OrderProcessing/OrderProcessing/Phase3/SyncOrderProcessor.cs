
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Phase1;

namespace OrderProcessing.Phase3;

public class SyncOrderProcessor
{
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncProductRepository _productRepository;
    private readonly SyncCouponRepository _couponRepository;
    private readonly SyncInventoryService _inventoryService;
    private readonly SyncOrderRepository _orderRepository;
    private readonly SyncPaymentService _paymentService;
    private readonly SyncEmailService _emailService;

    public SyncOrderProcessor(JsonFileStore store)
    {
        _customerRepository = new SyncCustomerRepository(store);
        _productRepository = new SyncProductRepository(store);
        _couponRepository = new SyncCouponRepository(store);

        _inventoryService = new SyncInventoryService(store);
        _orderRepository = new SyncOrderRepository(store);
        _paymentService = new SyncPaymentService(store);
        _emailService = new SyncEmailService(store);
    }

    public bool ProcessOrder(Order order)
    {
        // 1. Get customer
        Customer? customer =
            _customerRepository.GetById(order.CustomerId);

        if (customer is null || !customer.IsActive)
            return Reject(order, "Customer not found or inactive");

        // 2. Reserve stock
        bool stockReserved = _inventoryService.ReserveStock(order);

        if (!stockReserved)
            return Reject(order, "Insufficient stock");

        // 3. Calculate subtotal
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        decimal subtotal = 0;

        foreach (var item in order.Items)
        {
            Product? product =
                _productRepository.GetById(item.ProductId);

            if (product is null)
            {
                _inventoryService.ReleaseStock(
                    item.ProductId, item.Quantity);

                return Reject(order, $"Product #{item.ProductId} not found");
            }

            item.UnitPrice = product.Price;
            subtotal += product.Price * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subtotal, 2, MidpointRounding.AwayFromZero);

        // 4. Calculate discount
        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(500);

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
            Thread.Sleep(200);
            Thread.Sleep(Random.Shared.Next(300, 601));

            var coupon = _couponRepository.GetByCode(order.CouponCode);

            if (coupon is null || coupon.ExpiryDate < DateTime.Today)
            {
                foreach (var item in order.Items)
                {
                    _inventoryService.ReleaseStock(
                        item.ProductId,
                        item.Quantity);
                }

                return Reject(order, "Invalid or expired coupon");
            }

            couponDiscount = coupon.DiscountPercent;
        }

        decimal discountPercentage =
            Math.Min(loyaltyDiscount + couponDiscount, 30);

        order.Discount = Math.Round(
            order.SubTotal * discountPercentage / 100,
            2,
            MidpointRounding.AwayFromZero);

        // 5. Calculate shipping
        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(Random.Shared.Next(500, 1501));

        if (order.SubTotal >= 1000)
        {
            order.ShippingCost = 0;
        }
        else
        {
            int totalQuantity = 0;
            decimal totalWeight = 0;

            foreach (var item in order.Items)
            {
                var product =
                    _productRepository.GetById(item.ProductId);

                if (product is null)
                {
                    foreach (var reservedItem in order.Items)
                    {
                        _inventoryService.ReleaseStock(
                            reservedItem.ProductId,
                            reservedItem.Quantity);
                    }
                    return Reject(order, $"Product #{item.ProductId} not found");
                }

                totalQuantity += item.Quantity;
                totalWeight += product.WeightKg * item.Quantity;
            }

            order.ShippingCost = Math.Round(
                5m + (1.5m * totalQuantity) + (2m * totalWeight),
                2,
                MidpointRounding.AwayFromZero);
        }

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        // 6. Process payment
        string transactionId = _paymentService.ProcessPayment(order);

        order.TransactionId = transactionId;
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;

        // 7. Save order
        _orderRepository.Save(order);

        // 8. Send confirmation email
        _emailService.SendConfirmation(order, customer);

        return true;
    }

    private bool Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        _orderRepository.Save(order);

        return false;
    }
}
