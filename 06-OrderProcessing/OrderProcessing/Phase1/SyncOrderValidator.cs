using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncProductRepository _productRepository;
    private readonly SyncCouponRepository _couponRepository;

    public SyncOrderValidator(
        SyncCustomerRepository customerRepository,
        SyncProductRepository productRepository,
        SyncCouponRepository couponRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public Order ValidateOrder(Order order)
    {
        // 1. Check customer
        Customer? customer =
            _customerRepository.GetById(order.CustomerId);

        if (customer is null)
        {
            return Reject(order, $"Customer #{order.CustomerId} not found");
        }

        // 2. Check if customer is active
        if (!customer.IsActive)
        {
            return Reject(order, "Customer is inactive");
        }

        // 3. Check products and stock
        foreach (OrderItem item in order.Items)
        {
            Product? product =
                _productRepository.GetById(item.ProductId);

            if (product is null)
            {
                return Reject(order, $"Product #{item.ProductId} not found");
            }

            if (product.StockQuantity < item.Quantity)
            {
                return Reject(
                    order,
                    $"Insufficient stock for '{product.Name}'");
            }
        }

        // 4. Check coupon
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            Coupon? coupon =
                _couponRepository.GetByCode(order.CouponCode);

            if (coupon is null)
            {
                return Reject(
                    order,
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate < DateTime.Today)
            {
                return Reject(
                    order,
                    $"Coupon '{order.CouponCode}' has expired");
            }
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;

        return order;
    }

    private static Order Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        return order;
    }
}