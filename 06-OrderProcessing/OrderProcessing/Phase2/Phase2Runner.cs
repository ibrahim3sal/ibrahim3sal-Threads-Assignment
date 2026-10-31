using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Phase1;

namespace OrderProcessing.Phase2;

public class Phase2Runner
{
    private readonly JsonFileStore _store;
    private readonly DataSeeder _seeder;
    private readonly ProductRepository _productRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly CouponRepository _couponRepository;
    private readonly IncomingOrderRepository _incomingRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderValidator _orderValidator;
    private readonly OrderPricer _orderPricer;
    private readonly OrderProcessorV2 _orderProcessor;
    private readonly RaceConditionDemo _raceConditionDemo;
    private readonly ReportGenerator _reportGenerator;

    public Phase2Runner(
        JsonFileStore store,
        DataSeeder seeder,
        ProductRepository productRepository,
        CustomerRepository customerRepository,
        CouponRepository couponRepository,
        IncomingOrderRepository incomingRepository,
        OrderRepository orderRepository,
        OrderValidator orderValidator,
        OrderPricer orderPricer,
        OrderProcessorV2 orderProcessor,
        RaceConditionDemo raceConditionDemo,
        ReportGenerator reportGenerator)
    {
        _store = store;
        _seeder = seeder;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;
        _incomingRepository = incomingRepository;
        _orderRepository = orderRepository;
        _orderValidator = orderValidator;
        _orderPricer = orderPricer;
        _orderProcessor = orderProcessor;
        _raceConditionDemo = raceConditionDemo;
        _reportGenerator = reportGenerator;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Reset data
        await _seeder.ResetAsync();

        // 2. Race condition demonstration
        await _raceConditionDemo.RunAsync(cancellationToken);

        // 3. Reset data again
        await _seeder.ResetAsync();

        // 4. Pricing comparison
        Console.WriteLine("\n===== Pricing Comparison =====");

        List<Order> incomingOrders =
            await _incomingRepository.GetAllAsync(cancellationToken);

        Order originalOrder = incomingOrders.First(o => o.Id == 1);

        Order sequentialOrder = CloneOrder(originalOrder);
        Order concurrentOrder = CloneOrder(originalOrder);

        Stopwatch sequentialWatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderSequentialAsync(
            sequentialOrder,
            cancellationToken);

        sequentialWatch.Stop();

        Stopwatch concurrentWatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderAsync(
            concurrentOrder,
            cancellationToken);

        concurrentWatch.Stop();

        Console.WriteLine(
            $"Sequential Pricing: {sequentialWatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Concurrent Pricing: {concurrentWatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Order #1 Total: {concurrentOrder.TotalAmount:F2}");

        // 5. Validate all 20 orders concurrently
        Console.WriteLine("\n===== Concurrent Validation =====");

        Task<Order>[] validationTasks = incomingOrders
            .Select(order =>
                _orderValidator.ValidateOrderAsync(
                    order,
                    cancellationToken))
            .ToArray();

        foreach (Task<Order> task in validationTasks)
        {
            await task;
        }

        List<Order> validatedOrders =
            await _orderRepository.GetByStatusAsync(
                OrderStatus.Validated,
                cancellationToken);

        Console.WriteLine(
            $"Validated orders: {validatedOrders.Count}");

        // 6. Start processing all validated orders
        Console.WriteLine("\n===== Concurrent Order Processing =====");

        Stopwatch processingWatch = Stopwatch.StartNew();

        List<Task<bool>> processingTasks = validatedOrders
            .Select(order =>
                _orderProcessor.ProcessValidatedOrderAsync(
                    order,
                    cancellationToken))
            .ToList();

        if (processingTasks.Count > 0)
        {
            Console.WriteLine(
                $"First task status: {processingTasks[0].Status}");
        }

        // 7. Observe tasks while they are running
        await Task.Delay(1000, cancellationToken);

        Console.WriteLine("\nTask statuses after 1 second:");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            Console.WriteLine(
                $"Order #{validatedOrders[i].Id}: {processingTasks[i].Status}");
        }

        // 8. Await each task independently
        for (int i = 0; i < processingTasks.Count; i++)
        {
            Order order = validatedOrders[i];
            Task<bool> task = processingTasks[i];

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Order #{order.Id} failed: {ex.Message}");

                // Release reserved stock
                foreach (OrderItem item in order.Items)
                {
                    await _productRepository.ReleaseStockAsync(
                        item.ProductId,
                        item.Quantity,
                        cancellationToken);
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(
                    order,
                    cancellationToken);
            }

            Console.WriteLine(
                $"Order #{order.Id}: {task.Status}");
        }

        processingWatch.Stop();

        Console.WriteLine("\nFinal task statuses:");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            Console.WriteLine(
                $"Order #{validatedOrders[i].Id}: {processingTasks[i].Status}");
        }

        Console.WriteLine(
            $"Total Processing Time: {processingWatch.Elapsed.TotalSeconds:F2} seconds");

        // 9. Verify final order count
        List<Order> allOrders =
            await _orderRepository.GetAllAsync(cancellationToken);

        Console.WriteLine(
            $"\nFinal orders.json records: {allOrders.Count}");

        // 10. Generate report while displaying a message
        Console.WriteLine("\nGenerating report...");

        Task<Report> reportTask =
            _reportGenerator.GenerateAsync(cancellationToken);

        Report report = await reportTask;

        Console.WriteLine("\n===== Final Report =====");
        Console.WriteLine($"Total Orders: {report.TotalOrders}");
        Console.WriteLine($"Completed: {report.CompletedCount}");
        Console.WriteLine($"Rejected: {report.RejectedCount}");
        Console.WriteLine($"Failed: {report.FailedCount}");
        Console.WriteLine($"Payment Failed: {report.PaymentFailedCount}");
        Console.WriteLine($"Total Revenue: {report.TotalRevenue:F2}");
        Console.WriteLine($"Average Order Value: {report.AverageOrderValue:F2}");
        Console.WriteLine($"Top Selling Product: {report.TopSellingProduct}");
        Console.WriteLine($"Top Selling Quantity: {report.TopSellingQuantity}");
    }

    private static Order CloneOrder(Order order)
    {
        return new Order
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CouponCode = order.CouponCode,
            Items = order.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };
    }
}