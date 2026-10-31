using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    public async Task GenerateAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        Log.Info($"Generating invoice for Order #{order.Id}");

        // CPU-bound work
        await Task.Run(() =>
        {
            long result = 0;

            for (int i = 0; i < 20_000_000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result += i;
            }

            return result;
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        string invoiceDirectory =
            Path.Combine(DataFiles.DataDirectory, DataFiles.InvoicesFolder);

        Directory.CreateDirectory(invoiceDirectory);

        string filePath = Path.Combine(
            invoiceDirectory,
            $"invoice_{order.Id}.txt");

        string content =
            $"Invoice #{order.Id}{Environment.NewLine}" +
            $"Customer ID: {order.CustomerId}{Environment.NewLine}" +
            $"SubTotal: {order.SubTotal:F2}{Environment.NewLine}" +
            $"Discount: {order.Discount:F2}{Environment.NewLine}" +
            $"Shipping: {order.ShippingCost:F2}{Environment.NewLine}" +
            $"Total: {order.TotalAmount:F2}";

        await File.WriteAllTextAsync(
            filePath,
            content,
            cancellationToken);

        Log.Success($"Invoice generated for Order #{order.Id}");
    }
}