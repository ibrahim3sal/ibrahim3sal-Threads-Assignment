using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Product? GetById(int id)
    {
        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncProductRepository -> Executing query for Product #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Product> products =
            _store.ReadAll<Product>(DataFiles.Products);

        Product? product = products.FirstOrDefault(p => p.Id == id);

        if (product is null)
        {
            Log.Error($"Product #{id} not found.");
            return null;
        }

        Log.Success($"Product #{id} found ({product.Name})");

        return product;
    }
}
