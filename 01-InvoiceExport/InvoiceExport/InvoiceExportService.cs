using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public void ExportAllPending()
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders = _db.GetPendingOrders();

        foreach (Order order in orders)
        {
            Order fresh = _db.GetOrderById(order.Id)
                ?? throw new InvalidOperationException($"Order {order.Id} missing");

            CustomerProfile customer = _customers.ReadProfile(fresh.CustomerId);
            string path = _invoices.WriteInvoice(fresh, customer);
            Console.WriteLine($"Wrote {path}");
        }

        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}
