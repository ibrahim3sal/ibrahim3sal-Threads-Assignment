using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncInventoryService
{
    private readonly JsonFileStore _store;

    public SyncInventoryService(JsonFileStore store)
    {
        _store = store;
    }

    public bool ReserveStock(Order order)
    {
        var reservedItems = new List<(int ProductId, int Quantity)>();

        foreach (var item in order.Items)
        {
            Thread.Sleep(200);
            Thread.Sleep(100);
            Thread.Sleep(400);

            bool reserved = false;

            _store.Update<Product>(
                DataFiles.Products,
                products =>
                {
                    var product = products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is null ||
                        product.StockQuantity < item.Quantity)
                    {
                        return products;
                    }

                    product.StockQuantity -= item.Quantity;
                    reserved = true;

                    return products;
                });

            Thread.Sleep(200);

            if (!reserved)
            {
                foreach (var reservedItem in reservedItems)
                {
                    ReleaseStock(
                        reservedItem.ProductId,
                        reservedItem.Quantity);
                }

                return false;
            }

            reservedItems.Add((item.ProductId, item.Quantity));
        }

        return true;
    }

    public void ReleaseStock(int productId, int quantity)
    {
        Thread.Sleep(200);
        Thread.Sleep(100);
        Thread.Sleep(400);

        _store.Update<Product>(
            DataFiles.Products,
            products =>
            {
                var product = products.FirstOrDefault(
                    p => p.Id == productId);

                if (product is not null)
                {
                    product.StockQuantity += quantity;
                }

                return products;
            });

        Thread.Sleep(200);
    }
}