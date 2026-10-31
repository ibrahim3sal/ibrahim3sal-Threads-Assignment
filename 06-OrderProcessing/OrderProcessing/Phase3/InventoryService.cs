using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;

namespace OrderProcessing.Phase3;

public class InventoryService
{
    private readonly ProductRepository _productRepository;

    public InventoryService(ProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> ReserveStockAsync(
        Order order,
        CancellationToken ct = default)
    {
        var reservedItems = new List<OrderItem>();

        foreach (var item in order.Items)
        {
            ct.ThrowIfCancellationRequested();

            bool reserved = await _productRepository.ReserveStockAsync(
                item.ProductId,
                item.Quantity,
                ct);

            if (!reserved)
            {
                foreach (var reservedItem in reservedItems)
                {
                    await _productRepository.ReleaseStockAsync(
                        reservedItem.ProductId,
                        reservedItem.Quantity,
                        CancellationToken.None);
                }

                return false;
            }

            reservedItems.Add(item);
        }

        return true;
    }

    public async Task ReleaseStockAsync(
        Order order,
        CancellationToken ct = default)
    {
        foreach (var item in order.Items)
        {
            await _productRepository.ReleaseStockAsync(
                item.ProductId,
                item.Quantity,
                ct);
        }
    }
}