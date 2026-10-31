
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;

namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly CouponRepository _couponRepository;
    private readonly OrderRepository _orderRepository;

    public OrderValidator(
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        CouponRepository couponRepository,
        OrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
        _orderRepository = orderRepository;
    }

    public async Task<Order> ValidateOrderAsync(
        Order order,
        CancellationToken ct)
    {
        string? failureReason = null;

        // 1. Check customer exists
        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId, ct);

        if (customer is null)
        {
            failureReason = $"Customer #{order.CustomerId} not found";
        }
        // 2. Check customer is active
        else if (!customer.IsActive)
        {
            failureReason = "Customer is inactive";
        }

        // 3 & 4. Check products exist and stock is sufficient
        if (failureReason is null)
        {
            foreach (var item in order.Items)
            {
                var product = await _productRepository.GetByIdAsync(
                    item.ProductId, ct);

                if (product is null)
                {
                    failureReason = $"Product #{item.ProductId} not found";
                    break;
                }

                if (product.StockQuantity < item.Quantity)
                {
                    failureReason =
                        $"Insufficient stock for '{product.Name}'";
                    break;
                }
            }
        }

        // 5 & 6. Check coupon exists and is not expired
        if (failureReason is null && order.CouponCode is not null)
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode, ct);

            if (coupon is null)
            {
                failureReason =
                    $"Coupon '{order.CouponCode}' does not exist";
            }
            else if (coupon.ExpiryDate < DateTime.Today)
            {
                failureReason =
                    $"Coupon '{order.CouponCode}' has expired";
            }
        }

        // Set final status
        if (failureReason is null)
        {
            order.Status = OrderStatus.Validated;
            order.FailureReason = null;
        }
        else
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = failureReason;
        }

        // Save both successful and rejected orders
        await _orderRepository.SaveAsync(order, ct);

        return order;
    }
}
