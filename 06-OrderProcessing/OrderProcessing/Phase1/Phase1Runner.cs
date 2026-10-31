using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Repositories;

namespace OrderProcessing.Phase1;

public class Phase1Runner
{
    private readonly JsonFileStore _store;
    private readonly DataSeeder _seeder;

    public Phase1Runner(JsonFileStore store, DataSeeder seeder)
    {
        _store = store;
        _seeder = seeder;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Reset and load incoming orders
        await _seeder.ResetAsync();

        var incomingRepository = new IncomingOrderRepository(_store);
        List<Order> orders =
            await incomingRepository.GetAllAsync(cancellationToken);

        // Sync run (results are not saved)
        var syncCustomer = new SyncCustomerRepository(_store);
        var syncProduct = new SyncProductRepository(_store);
        var syncCoupon = new SyncCouponRepository(_store);

        var syncValidator = new SyncOrderValidator(
            syncCustomer,
            syncProduct,
            syncCoupon);

        Stopwatch syncStopwatch = Stopwatch.StartNew();

        foreach (Order order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            syncValidator.ValidateOrder(order);
        }

        syncStopwatch.Stop();

        // Reset before async run
        await _seeder.ResetAsync();

        // Async run
        var customerRepository = new CustomerRepository(_store);
        var productRepository = new ProductRepository(_store);
        var couponRepository = new CouponRepository(_store);
        var orderRepository = new OrderRepository(_store);

        var asyncValidator = new OrderValidator(
            customerRepository,
            productRepository,
            couponRepository,
            orderRepository);

        Stopwatch asyncStopwatch = Stopwatch.StartNew();

        foreach (Order order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await asyncValidator.ValidateOrderAsync(
                order,
                cancellationToken);
        }

        asyncStopwatch.Stop();

        // Read results back from orders.json
        List<Order> savedOrders =
            await orderRepository.GetAllAsync(cancellationToken);

        List<Order> validatedOrders =
            await orderRepository.GetByStatusAsync(
                OrderStatus.Validated,
                cancellationToken);

        List<Order> rejectedOrders =
            await orderRepository.GetByStatusAsync(
                OrderStatus.Rejected,
                cancellationToken);

        // Print summary
        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {savedOrders.Count}");
        Console.WriteLine($"Validated:    {validatedOrders.Count}");
        Console.WriteLine($"Rejected:     {rejectedOrders.Count}");

        foreach (Order order in rejectedOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"  - Order #{order.Id}: {order.FailureReason}");
        }

        Console.WriteLine($"Sync Time:  {syncStopwatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"Async Time: {asyncStopwatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {savedOrders.Count} records.");
    }
}