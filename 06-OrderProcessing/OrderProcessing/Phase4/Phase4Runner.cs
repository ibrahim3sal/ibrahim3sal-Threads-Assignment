using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4.Shipping;
using OrderProcessing.Shared.Exceptions;
using System.Collections.Concurrent;

namespace OrderProcessing.Phase4;

public class Phase4Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderProcessor _orderProcessor;

    public Phase4Runner(
        DataSeeder seeder,
        IncomingOrderRepository incomingRepository,
        OrderRepository orderRepository,
        OrderProcessor orderProcessor)
    {
        _seeder = seeder;
        _incomingRepository = incomingRepository;
        _orderRepository = orderRepository;
        _orderProcessor = orderProcessor;
    }

    public async Task RunLiveProgressAsync(CancellationToken ct = default)
    {
        await _seeder.ResetAsync();

        List<Order> orders = await _incomingRepository.GetAllAsync(ct);

        Console.WriteLine(
            $"\n===== Phase 4 - Task 10.1: Live Checkout Progress ({orders.Count} Orders) =====");

        Stopwatch stopwatch = Stopwatch.StartNew();

        List<Task<bool>> tasks = orders
            .Select(order => _orderProcessor.ProcessOrderAsync(order, ct))
            .ToList();

        Dictionary<Task<bool>, Order> orderByTask = tasks
            .Select((task, index) => new { task, order = orders[index] })
            .ToDictionary(x => x.task, x => x.order);

        Console.WriteLine($"Started {tasks.Count} orders.");

        int completedCount = 0;

        await foreach (Task<bool> completedTask in Task.WhenEach(tasks))
        {
            Order order = orderByTask[completedTask];
            completedCount++;

            try
            {
                bool result = await completedTask;

                string status = result
                    ? "Completed"
                    : order.Status.ToString();

                string reason = !result &&
                                !string.IsNullOrWhiteSpace(order.FailureReason)
                    ? $" ({order.FailureReason})"
                    : "";

                Console.WriteLine(
                    $"[{completedCount,2}/{orders.Count}] " +
                    $"Order #{order.Id} -> {status}{reason} " +
                    $"after {stopwatch.Elapsed.TotalSeconds:F1} s");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[{completedCount,2}/{orders.Count}] " +
                    $"Order #{order.Id} -> Failed ({ex.Message}) " +
                    $"after {stopwatch.Elapsed.TotalSeconds:F1} s");
            }
        }

        stopwatch.Stop();

        List<Order> finalOrders = await _orderRepository.GetAllAsync(ct);

        Console.WriteLine("\n===== Task 10.1 Final Summary =====");
        Console.WriteLine($"Total Orders: {finalOrders.Count}");
        Console.WriteLine($"Completed: {finalOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine($"Rejected: {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine($"Payment Failed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine($"Failed: {finalOrders.Count(o => o.Status == OrderStatus.Failed)}");
        Console.WriteLine($"Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }

    public async Task RunDashboardAsync(
        CustomerDashboardService dashboardService,
        CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Task 10.2: Customer Dashboard =====");

        Console.WriteLine("\n--- Customer #3 ---");

        try
        {
            CustomerDashboard dashboard =
                await dashboardService.LoadAsync(3, ct);

            Console.WriteLine($"Customer: {dashboard.Customer.Name}");
            Console.WriteLine($"Orders: {dashboard.Orders.Count}");

            foreach (Order order in dashboard.Orders)
            {
                Console.WriteLine(
                    $"Order #{order.Id} -> {order.Status}");
            }

            Console.WriteLine($"Payments: {dashboard.Payments.Count}");

            foreach (PaymentTransaction payment in dashboard.Payments)
            {
                Console.WriteLine(
                    $"Order #{payment.OrderId} -> {payment.Status}");
            }

            Console.WriteLine($"Emails: {dashboard.Emails.Count}");

            foreach (EmailMessage email in dashboard.Emails)
            {
                Console.WriteLine(
                    $"Order #{email.OrderId} -> {email.Subject}");
            }
        }
        catch (AggregateException ex)
        {
            foreach (Exception inner in ex.InnerExceptions)
                Console.WriteLine($"Error: {inner.Message}");
        }

        Console.WriteLine("\n--- Customer #99 ---");

        try
        {
            await dashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException ex)
        {
            foreach (Exception inner in ex.InnerExceptions)
                Console.WriteLine($"Error: {inner.Message}");
        }
    }

    public async Task RunShippingQuotesAsync(
        CancellationToken ct = default)
    {
        Console.WriteLine(
            "\n===== Task 10.3: Fastest Shipping Quote =====");

        // Read the orders processed in Task 10.1.
        List<Order> orders = await _orderRepository.GetAllAsync(ct);

        // Normal carrier configuration.
        ShippingQuoteService quoteService = new(
            new IShippingCarrier[]
            {
                new FastShipCarrier(),
                new CairoExpressCarrier(),
                new NileCourierCarrier()
            });

        // Run orders #1 to #5.
        foreach (int orderId in Enumerable.Range(1, 5))
        {
            ct.ThrowIfCancellationRequested();

            Order? order = orders.FirstOrDefault(o => o.Id == orderId);

            if (order is null)
            {
                Console.WriteLine($"Order #{orderId} not found.");
                continue;
            }

            Console.WriteLine($"\n--- Order #{order.Id} ---");

            try
            {
                ShippingQuote quote =
                    await quoteService.GetFastestQuoteAsync(order, ct);

                Console.WriteLine($"Winner: {quote.Carrier}");
                Console.WriteLine($"Price: {quote.Price:F2}");
                Console.WriteLine($"Time: {quote.ElapsedMs} ms");
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Console.WriteLine($"Shipping failed: {ex.Message}");
            }
        }

        // Special test: all carriers fail for order #6.
        Console.WriteLine("\n--- Order #6: All Carriers Fail ---");

        Order? testOrder = orders.FirstOrDefault(o => o.Id == 6);

        if (testOrder is null)
        {
            Console.WriteLine("Order #6 not found.");
            return;
        }

        ShippingQuoteService failingQuoteService = new(
            new IShippingCarrier[]
            {
                new FastShipCarrier(new[] { 6 }),
                new CairoExpressCarrier(new[] { 6 }),
                new NileCourierCarrier(new[] { 6 })
            });

        try
        {
            ShippingQuote quote =
                await failingQuoteService.GetFastestQuoteAsync(
                    testOrder, ct);

            Console.WriteLine($"Winner: {quote.Carrier}");
            Console.WriteLine($"Price: {quote.Price:F2}");
            Console.WriteLine($"Time: {quote.ElapsedMs} ms");
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Console.WriteLine($"Expected error: {ex.Message}");
        }
    }
    public void RunFraudScoring(CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Task 10.4 A: Fraud Scoring =====");

        // Create 10,000 orders in memory.
        List<Order> orders = Enumerable.Range(1, 10_000)
            .Select(id => new Order { Id = id })
            .ToList();

        // Sequential execution.
        Stopwatch sequentialTimer = Stopwatch.StartNew();

        List<int> sequentialFlagged = new();

        foreach (Order order in orders)
        {
            int score = Score(order.Id);

            if (score >= 90)
                sequentialFlagged.Add(order.Id);
        }

        sequentialTimer.Stop();

        // Parallel execution.
        ConcurrentBag<int> parallelFlagged = new();
        ConcurrentDictionary<int, byte> usedThreads = new();

        Stopwatch parallelTimer = Stopwatch.StartNew();

        Parallel.ForEach(
            orders,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            },
            order =>
            {
                int threadId = Environment.CurrentManagedThreadId;
                usedThreads.TryAdd(threadId, 0);

                int score = Score(order.Id);

                if (score >= 90)
                    parallelFlagged.Add(order.Id);
            });

        parallelTimer.Stop();

        // Compare results.
        List<int> sortedParallel = parallelFlagged
            .OrderBy(id => id)
            .ToList();

        bool sameResults = sequentialFlagged
            .OrderBy(id => id)
            .SequenceEqual(sortedParallel);

        double speedUp = parallelTimer.Elapsed.TotalMilliseconds > 0
            ? sequentialTimer.Elapsed.TotalMilliseconds /
            parallelTimer.Elapsed.TotalMilliseconds
            : 0;

        Console.WriteLine("\n--- Sequential ---");
        Console.WriteLine($"Flagged Orders: {sequentialFlagged.Count}");
        Console.WriteLine($"Time: {sequentialTimer.ElapsedMilliseconds} ms");

        Console.WriteLine("\n--- Parallel ---");
        Console.WriteLine($"Flagged Orders: {parallelFlagged.Count}");
        Console.WriteLine($"Time: {parallelTimer.ElapsedMilliseconds} ms");
        Console.WriteLine($"Distinct Threads Used: {usedThreads.Count}");
        Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
        Console.WriteLine($"Speed-up: {speedUp:F2}x");

        Console.WriteLine("\n--- Validation ---");
        Console.WriteLine($"Same Results: {sameResults}");
        Console.WriteLine(
            $"First 5 Flagged IDs: {string.Join(", ", sortedParallel.Take(5))}");
        Console.WriteLine(
            $"Unique Flagged IDs: {sortedParallel.Distinct().Count()}");
    }

    private static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;

        return (int)(h % 100);
    }   
    public async Task RunEmailResendAsync(
    IEmailService emailService,
    CustomerService customerService,
    CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Task 10.4 B: Email Re-send =====");

        Customer customer = await customerService.GetCustomerAsync(1, ct);

        List<Order> testOrders = Enumerable.Range(1001, 30)
            .Select(id => new Order
            {
                Id = id,
                CustomerId = customer.Id,
                TotalAmount = 100
            })
            .ToList();

        int currentSends = 0;
        int maximumSends = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            testOrders,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = ct
            },
            async (order, token) =>
            {
                int current = Interlocked.Increment(ref currentSends);

                int observedMaximum;

                do
                {
                    observedMaximum = maximumSends;

                    if (current <= observedMaximum)
                        break;

                } while (Interlocked.CompareExchange(
                    ref maximumSends,
                    current,
                    observedMaximum) != observedMaximum);

                try
                {
                    await emailService.SendConfirmationAsync(
                        order,
                        customer,
                        token);
                }
                finally
                {
                    Interlocked.Decrement(ref currentSends);
                }
            });

        stopwatch.Stop();

        Console.WriteLine("\n--- Email Re-send Summary ---");
        Console.WriteLine($"Total Emails: {testOrders.Count}");
        Console.WriteLine($"Maximum Concurrent Sends: {maximumSends}");
        Console.WriteLine($"Total Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }
}