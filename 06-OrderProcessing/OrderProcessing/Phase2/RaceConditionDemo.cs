using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class RaceConditionDemo
{
    private readonly JsonFileStore _store;
    private readonly ProductRepository _productRepository;

    public RaceConditionDemo(
        JsonFileStore store,
        ProductRepository productRepository)
    {
        _store = store;
        _productRepository = productRepository;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n===== Unsafe Race Condition Demo =====");

        await ResetKeyboardStockAsync(cancellationToken);

        Task<bool> order10Task =
            _productRepository.ReserveStockUnsafeAsync(4, 1, cancellationToken);

        Task<bool> order11Task =
            _productRepository.ReserveStockUnsafeAsync(4, 1, cancellationToken);

        bool order10Result = await order10Task;
        bool order11Result = await order11Task;

        Product? unsafeProduct = await GetKeyboardAsync(cancellationToken);

        Console.WriteLine($"Order #10 reservation: {order10Result}");
        Console.WriteLine($"Order #11 reservation: {order11Result}");
        Console.WriteLine($"Remaining stock: {unsafeProduct?.StockQuantity}");

        Console.WriteLine("\n===== Safe Race Condition Demo =====");

        await ResetKeyboardStockAsync(cancellationToken);

        Task<bool> safeOrder10Task =
            _productRepository.ReserveStockAsync(4, 1, cancellationToken);

        Task<bool> safeOrder11Task =
            _productRepository.ReserveStockAsync(4, 1, cancellationToken);

        bool safeOrder10Result = await safeOrder10Task;
        bool safeOrder11Result = await safeOrder11Task;

        Product? safeProduct = await GetKeyboardAsync(cancellationToken);

        Console.WriteLine($"Order #10 reservation: {safeOrder10Result}");
        Console.WriteLine($"Order #11 reservation: {safeOrder11Result}");
        Console.WriteLine($"Remaining stock: {safeProduct?.StockQuantity}");
    }

    private async Task ResetKeyboardStockAsync(
        CancellationToken cancellationToken)
    {
        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? keyboard =
                    products.FirstOrDefault(p => p.Id == 4);

                if (keyboard is not null)
                    keyboard.StockQuantity = 1;

                return products;
            },
            cancellationToken);
    }

    private async Task<Product?> GetKeyboardAsync(
        CancellationToken cancellationToken)
    {
        List<Product> products =
            await _store.ReadAllAsync<Product>(
                DataFiles.Products,
                cancellationToken);

        return products.FirstOrDefault(p => p.Id == 4);
    }
}