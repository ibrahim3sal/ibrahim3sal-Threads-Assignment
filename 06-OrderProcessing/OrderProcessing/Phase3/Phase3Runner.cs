
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace OrderProcessing.Phase3;

public class Phase3Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderProcessor _orderProcessor;
    private readonly SyncOrderProcessor _syncOrderProcessor;

    public Phase3Runner(
        DataSeeder seeder,
        IncomingOrderRepository incomingRepository,
        OrderRepository orderRepository,
        OrderProcessor orderProcessor,
        SyncOrderProcessor syncOrderProcessor)
    {
        _seeder = seeder;
        _incomingRepository = incomingRepository;
        _orderRepository = orderRepository;
        _orderProcessor = orderProcessor;
        _syncOrderProcessor = syncOrderProcessor;
    }

    public async Task RunAsync(
        CancellationToken ct = default)
    {
        // 1. Reset data
        await _seeder.ResetAsync();

        // 2. Load incoming orders
        List<Order> orders =
            await _incomingRepository.GetAllAsync(ct);

        Console.WriteLine(
            $"\n===== Phase 3: Order Processing ({orders.Count} Orders) =====");

        // 3. Start all orders concurrently
        Stopwatch stopwatch = Stopwatch.StartNew();

        List<Task<bool>> processingTasks = orders
            .Select(order =>
                _orderProcessor.ProcessOrderAsync(order, ct))
            .ToList();

        Console.WriteLine(
            $"Started {processingTasks.Count} order processing tasks.");

        // 4. Await every task
        int successful = 0;
        int unsuccessful = 0;

        for (int i = 0; i < processingTasks.Count; i++)
        {
            try
            {
                bool result = await processingTasks[i];

                if (result)
                    successful++;
                else
                    unsuccessful++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                unsuccessful++;

                Log.Error(
                    $"Order #{orders[i].Id} failed unexpectedly: {ex.Message}");
            }
        }

        stopwatch.Stop();

        // 5. Read final orders
        List<Order> finalOrders =
            await _orderRepository.GetAllAsync(ct);

        // 6. Display results
        Console.WriteLine("\n===== Phase 3 Results =====");

        Console.WriteLine($"Total Orders: {finalOrders.Count}");
        Console.WriteLine($"Successful: {successful}");
        Console.WriteLine($"Unsuccessful: {unsuccessful}");

        Console.WriteLine(
            $"Completed: {finalOrders.Count(o => o.Status == OrderStatus.Completed)}");

        Console.WriteLine(
            $"Rejected: {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}");

        Console.WriteLine(
            $"Payment Failed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");

        Console.WriteLine(
            $"Failed: {finalOrders.Count(o => o.Status == OrderStatus.Failed)}");

        Console.WriteLine(
            $"Processing Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // 7. Display each order
        Console.WriteLine("\n===== Order Details =====");

        foreach (Order order in finalOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"Order #{order.Id,-3} | {order.Status,-13} | {order.FailureReason ?? "No errors"}");
        }
    }

    public async Task RunSyncAsync()
    {
        await _seeder.ResetAsync();

        List<Order> orders =
            (await _incomingRepository.GetAllAsync(CancellationToken.None))
            .Where(o => o.Id >= 1 && o.Id <= 10)
            .ToList();

        var threads = new List<Thread>();
        var usedThreads = new ConcurrentDictionary<int, byte>();

        int successful = 0;
        int unsuccessful = 0;

        int threadPoolBefore = ThreadPool.ThreadCount;

        Stopwatch stopwatch = Stopwatch.StartNew();

        foreach (Order order in orders)
        {
            Thread thread = new Thread(() =>
            {
                usedThreads.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                try
                {
                    bool result = _syncOrderProcessor.ProcessOrder(order);

                    if (result)
                        Interlocked.Increment(ref successful);
                    else
                        Interlocked.Increment(ref unsuccessful);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref unsuccessful);
                    Console.WriteLine(
                        $"Order #{order.Id}: {ex.Message}");
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (Thread thread in threads)
            thread.Join();

        stopwatch.Stop();

        Console.WriteLine("\n===== Sync Results =====");
        Console.WriteLine($"Successful: {successful}");
        Console.WriteLine($"Unsuccessful: {unsuccessful}");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Threads Used: {usedThreads.Count}");
        Console.WriteLine($"ThreadPool.ThreadCount Before: {threadPoolBefore}");
        Console.WriteLine($"ThreadPool.ThreadCount After: {ThreadPool.ThreadCount}");
    }

    
    public async Task RunAsyncComparison()
    {
        await _seeder.ResetAsync();

        List<Order> orders = (await _incomingRepository.GetAllAsync(
            CancellationToken.None))
            .Where(o => o.Id >= 1 && o.Id <= 10)
            .ToList();

        var usedThreads = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();

        int successful = 0;
        int unsuccessful = 0;

        int threadPoolBefore = ThreadPool.ThreadCount;

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Start all 10 tasks
        List<Task<bool>> tasks = orders.Select(async order =>
        {
            usedThreads.TryAdd(
                Environment.CurrentManagedThreadId, 0);

            try
            {
                bool result = await _orderProcessor.ProcessOrderAsync(
                    order, CancellationToken.None);

                usedThreads.TryAdd(
                    Environment.CurrentManagedThreadId, 0);

                if (result)
                    Interlocked.Increment(ref successful);
                else
                    Interlocked.Increment(ref unsuccessful);

                return result;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref unsuccessful);

                Console.WriteLine(
                    $"Order #{order.Id}: {ex.Message}");

                return false;
            }
        }).ToList();

        // Await all tasks
        await Task.WhenAll(tasks);

        stopwatch.Stop();

        Console.WriteLine("\n===== Async Results =====");
        Console.WriteLine($"Successful: {successful}");
        Console.WriteLine($"Unsuccessful: {unsuccessful}");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Threads Observed: {usedThreads.Count}");
        Console.WriteLine($"ThreadPool.ThreadCount Before: {threadPoolBefore}");
        Console.WriteLine($"ThreadPool.ThreadCount After: {ThreadPool.ThreadCount}");
    }

}
