
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Models;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;

internal class Program
{
    private static async Task Main(string[] args)
    {
        string mode = args.Length > 0
            ? args[0].ToLowerInvariant()
            : ShowMenu();

        int? cancelAfterMs = GetCancelAfter(args);

        using var cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCancellation requested...");
        };

        Console.CancelKeyPress += cancelHandler;

        if (cancelAfterMs.HasValue)
            cts.CancelAfter(cancelAfterMs.Value);

        try
        {
            await RunAsync(mode, ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nRun cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nUnexpected error: {ex}");
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static string ShowMenu()
    {
        Console.WriteLine("\n===== Order Processing System =====");
        Console.WriteLine("1. Phase 1 - Validation");
        Console.WriteLine("2. Phase 2 - Pricing and Processing");
        Console.WriteLine("3. Phase 3 - Full Checkout");
        Console.WriteLine("4. Phase 3 - Sync vs Async Comparison");
        Console.WriteLine("5. Phase 4 - Scale-Up");
        Console.WriteLine("6. Run All");
        Console.Write("\nChoose an option: ");

        return Console.ReadLine() switch
        {
            "1" => "phase1",
            "2" => "phase2",
            "3" => "phase3",
            "4" => "phase3-comparison",
            "5" => "phase4",
            "6" => "all",
            _ => "invalid"
        };
    }

    private static int? GetCancelAfter(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--cancel-after" &&
                int.TryParse(args[i + 1], out int milliseconds) &&
                milliseconds > 0)
            {
                return milliseconds;
            }
        }

        return null;
    }

    private static async Task RunAsync(
        string mode,
        CancellationToken ct)
    {
        var store = new JsonFileStore();
        var seeder = new DataSeeder(store);

        // Repositories
        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);
        var incomingOrderRepository = new IncomingOrderRepository(store);
        var orderRepository = new OrderRepository(store);

        // Phase 1
        var orderValidator = new OrderValidator(
            customerRepository,
            productRepository,
            couponRepository,
            orderRepository);

        var phase1Runner = new Phase1Runner(store, seeder);

        // Phase 2
        var pricingService = new PricingService(
            productRepository,
            customerRepository,
            couponRepository,
            new[] { 7 });

        var orderPricer = new OrderPricer(pricingService);
        
        var invoiceGenerator = new InvoiceGenerator();

        var orderProcessorV2 = new OrderProcessorV2(
            productRepository,
            orderRepository,
            orderPricer,
            invoiceGenerator);

        var raceConditionDemo = new RaceConditionDemo(
            store,
            productRepository);

        var reportGenerator = new ReportGenerator(
            orderRepository,
            productRepository);

        var phase2Runner = new Phase2Runner(
            store,
            seeder,
            productRepository,
            customerRepository,
            couponRepository,
            incomingOrderRepository,
            orderRepository,
            orderValidator,
            orderPricer,
            orderProcessorV2,
            raceConditionDemo,
            reportGenerator);
            

        // Phase 3 services
        var customerService = new CustomerService(customerRepository);
        var inventoryService = new InventoryService(productRepository);

        var phase3PricingService = new PricingService(
            productRepository,
            customerRepository,
            couponRepository,
            Array.Empty<int>());

        var paymentGateway = new PaymentGateway(
            store,
            timeoutOrderIds: new[] { 5 },
            declineOrderIds: new[] { 8 });

        var emailService = new EmailService(
            store,
            failAttempts: new Dictionary<int, int>
            {
                [3] = 2,
                [17] = int.MaxValue
            });

        var phase3OrderRepository = new OrderRepository(
            store,
            failSaveForOrderIds: new[] { 13 });

        var phase3OrderProcessor = new OrderProcessor(
            customerService,
            inventoryService,
            phase3PricingService,
            paymentGateway,
            emailService,
            phase3OrderRepository);

        // Phase 3 comparison services
        var syncOrderProcessor = new SyncOrderProcessor(store);

        var comparisonOrderRepository = new OrderRepository(
            store,
            enableFailures: false);

        var comparisonPricingService = new PricingService(
            productRepository,
            customerRepository,
            couponRepository,
            enableFailures: false);

        var comparisonPaymentGateway = new PaymentGateway(
            store,
            enableFailures: false);

        var comparisonEmailService = new EmailService(
            store,
            enableFailures: false);

        var comparisonOrderProcessor = new OrderProcessor(
            customerService,
            inventoryService,
            comparisonPricingService,
            comparisonPaymentGateway,
            comparisonEmailService,
            comparisonOrderRepository);

    // Normal Phase 3 with configured failures
    var phase3Runner = new Phase3Runner(
        seeder,
        incomingOrderRepository,
        phase3OrderRepository,
        phase3OrderProcessor,
        syncOrderProcessor);

    // Sync vs Async comparison with failures disabled
    var comparisonRunner = new Phase3Runner(
        seeder,
        incomingOrderRepository,
        comparisonOrderRepository,
        comparisonOrderProcessor,
        syncOrderProcessor);

        // Phase 4
        var phase4Runner = new Phase4Runner(
            seeder,
            incomingOrderRepository,
            phase3OrderRepository,
            phase3OrderProcessor);

        var dashboardService = new CustomerDashboardService(
            customerRepository,
            phase3OrderRepository,
            store,
            failPaymentsForCustomerIds: new[] { 99 });

        var phase4EmailService = new EmailService(
            store,
            enableFailures: false,
            sendDelayMs: 1000);

        var phase4CustomerService = new CustomerService(
            customerRepository);

        // Execute selected mode
        switch (mode)
        {
            case "phase1":
                await phase1Runner.RunAsync(ct);
                break;

            case "phase2":
                await phase2Runner.RunAsync(ct);
                break;

            case "phase3":
                await phase3Runner.RunAsync();
                break;

            case "phase3-comparison":
                await comparisonRunner.RunSyncAsync();
                await comparisonRunner.RunAsyncComparison();
                break;
            case "phase4":
                await RunPhase4Async(
                    phase4Runner,
                    dashboardService,
                    phase4EmailService,
                    phase4CustomerService,
                    ct);
                break;

            case "all":
                await phase1Runner.RunAsync(ct);
                await phase2Runner.RunAsync(ct);
                await phase3Runner.RunAsync(ct);

                await RunPhase4Async(
                    phase4Runner,
                    dashboardService,
                    phase4EmailService,
                    phase4CustomerService,
                    ct);
                break;

            default:
                Console.WriteLine("Invalid option.");
                break;
        }
    }

    private static async Task RunPhase4Async(
        Phase4Runner runner,
        CustomerDashboardService dashboard,
        EmailService emailService,
        CustomerService customerService,
        CancellationToken ct)
    {
        await runner.RunLiveProgressAsync(ct);
        await runner.RunDashboardAsync(dashboard, ct);
        await runner.RunShippingQuotesAsync(ct);

        runner.RunFraudScoring(ct);

        await runner.RunEmailResendAsync(
            emailService,
            customerService,
            ct);
    }
}
