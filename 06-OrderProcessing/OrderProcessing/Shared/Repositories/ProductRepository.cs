
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Shared.Repositories;

public class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for Product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products =
            await _store.ReadAllAsync<Product>(DataFiles.Products, ct);

        Product? product = products.FirstOrDefault(p => p.Id == id);

        if (product is null)
        {
            Log.Error($"Product #{id} not found.");
            return null;
        }

        Log.Success($"Product #{id} found ({product.Name})");
        return product;
    }

    public async Task<bool> ReserveStockAsync(
        int productId,
        int qty,
        CancellationToken ct)
    {
        if (qty <= 0)
            throw new ArgumentOutOfRangeException(nameof(qty));

        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        bool reserved = false;

        Log.Info($"ProductRepository -> Updating stock for Product #{productId}...");
        await Task.Delay(400, ct);

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? product = products.FirstOrDefault(p => p.Id == productId);

                if (product is null || product.StockQuantity < qty)
                    return products;

                product.StockQuantity -= qty;
                reserved = true;

                return products;
            },
            ct);

        Log.Info("ProductRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        if (reserved)
            Log.Success($"Reserved {qty} units of Product #{productId}.");
        else
            Log.Error($"Could not reserve stock for Product #{productId}.");

        return reserved;
    }

    public async Task<bool> ReserveStockUnsafeAsync(
    int productId,
    int qty,
    CancellationToken cancellationToken = default)
    {
        Log.Info($"Unsafe reservation started for Product #{productId}");

        Log.Info("Opening connection...");
        await Task.Delay(200, cancellationToken);

        Log.Info("Beginning transaction...");
        await Task.Delay(100, cancellationToken);

        List<Product> products =
            await _store.ReadAllAsync<Product>(
                DataFiles.Products,
                cancellationToken);

        Product? product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < qty)
            return false;

        int availableStock = product.StockQuantity;

        Log.Info("Updating stock...");
        await Task.Delay(400, cancellationToken);

        // Intentionally unsafe: no synchronization.
        product.StockQuantity = availableStock - qty;

        await _store.WriteAllAsync(
            DataFiles.Products,
            products,
            cancellationToken);

        Log.Success($"Unsafe reservation completed for Product #{productId}");

        return true;
    }

    public async Task ReleaseStockAsync(
    int productId,
    int qty,
    CancellationToken cancellationToken = default)
    {
        if (qty <= 0)
            throw new ArgumentOutOfRangeException(nameof(qty));

        Log.Info($"Releasing {qty} units of Product #{productId}");

        Log.Info("Opening connection...");
        await Task.Delay(200, cancellationToken);

        Log.Info("Beginning transaction...");
        await Task.Delay(100, cancellationToken);

        Log.Info("Updating stock...");
        await Task.Delay(400, cancellationToken);

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? product =
                    products.FirstOrDefault(p => p.Id == productId);

                if (product is not null)
                    product.StockQuantity += qty;

                return products;
            },
            cancellationToken);

        Log.Success($"Stock released for Product #{productId}");

        Log.Info("Committing transaction...");
        await Task.Delay(200, cancellationToken);
    }
}
