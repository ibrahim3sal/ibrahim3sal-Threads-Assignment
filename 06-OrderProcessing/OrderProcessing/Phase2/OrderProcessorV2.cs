using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public class OrderProcessorV2
{
    private readonly ProductRepository _productRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;

    public OrderProcessorV2(
        ProductRepository productRepository,
        OrderRepository orderRepository,
        OrderPricer orderPricer,
        InvoiceGenerator invoiceGenerator)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _orderPricer = orderPricer;
        _invoiceGenerator = invoiceGenerator;
    }

    public async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        Log.Info($"Processing Order #{order.Id}");

        var reservedItems = new List<OrderItem>();

        // Step 1: Reserve stock
        foreach (OrderItem item in order.Items)
        {
            bool reserved = await _productRepository.ReserveStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);

            if (!reserved)
            {
                Product? product =
                    await _productRepository.GetByIdAsync(
                        item.ProductId,
                        cancellationToken);

                // Release previously reserved items
                foreach (OrderItem reservedItem in reservedItems)
                {
                    await _productRepository.ReleaseStockAsync(
                        reservedItem.ProductId,
                        reservedItem.Quantity,
                        cancellationToken);
                }

                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{product?.Name ?? $"Product #{item.ProductId}"}'";

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);

                Log.Error(
                    $"Order #{order.Id} rejected: {order.FailureReason}");

                return false;
            }

            reservedItems.Add(item);
        }

        // Step 2: Calculate pricing
        await _orderPricer.PriceOrderAsync(
            order,
            cancellationToken);

        // Step 3: Generate invoice
        await _invoiceGenerator.GenerateAsync(
            order,
            cancellationToken);

        // Step 4: Complete order
        order.Status = OrderStatus.Completed;
        order.FailureReason = null;

        await _orderRepository.SaveAsync(
            order,
            cancellationToken);

        Log.Success($"Order #{order.Id} completed successfully.");

        return true;
    }
}