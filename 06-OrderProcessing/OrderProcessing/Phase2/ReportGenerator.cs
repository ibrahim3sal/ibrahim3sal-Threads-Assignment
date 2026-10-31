using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public class ReportGenerator
{
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ReportGenerator(
        OrderRepository orderRepository,
        ProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<Report> GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        Log.Info("Generating sales report...");

        // Load all orders
        List<Order> orders =
            await _orderRepository.GetAllAsync(cancellationToken);

        List<Order> completedOrders = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .ToList();

        int completedCount = completedOrders.Count;

        decimal totalRevenue = completedOrders.Sum(o => o.TotalAmount);

        decimal averageOrderValue = completedCount == 0
            ? 0
            : totalRevenue / completedCount;

        // Find the top-selling product
        var topSelling = completedOrders
            .SelectMany(o => o.Items)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductId)
            .FirstOrDefault();

        string topSellingProduct = "N/A";
        int topSellingQuantity = 0;

        if (topSelling is not null)
        {
            Product? product =
                await _productRepository.GetByIdAsync(
                    topSelling.ProductId,
                    cancellationToken);

            topSellingProduct = product?.Name ?? "N/A";
            topSellingQuantity = topSelling.Quantity;
        }

        var report = new Report
        {
            TotalOrders = orders.Count,
            TotalRevenue = Math.Round(
                totalRevenue, 2, MidpointRounding.AwayFromZero),

            CompletedCount = completedCount,

            RejectedCount = orders.Count(
                o => o.Status == OrderStatus.Rejected),

            FailedCount = orders.Count(
                o => o.Status == OrderStatus.Failed),

            PaymentFailedCount = orders.Count(
                o => o.Status == OrderStatus.PaymentFailed),

            TopSellingProduct = topSellingProduct,
            TopSellingQuantity = topSellingQuantity,

            AverageOrderValue = Math.Round(
                averageOrderValue, 2, MidpointRounding.AwayFromZero),

            GeneratedAt = DateTime.Now
        };

        // Save report
        string reportsDirectory = Path.Combine(
            DataFiles.DataDirectory,
            DataFiles.ReportsFolder);

        Directory.CreateDirectory(reportsDirectory);

        string fileName =
            $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json";

        string filePath = Path.Combine(
            reportsDirectory,
            fileName);

        string json = JsonSerializer.Serialize(report, JsonOptions);

        await File.WriteAllTextAsync(
            filePath,
            json,
            cancellationToken);

        Log.Success($"Report saved: {fileName}");

        return report;
    }
}