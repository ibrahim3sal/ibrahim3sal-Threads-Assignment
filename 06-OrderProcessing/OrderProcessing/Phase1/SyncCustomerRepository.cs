
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Customer? GetById(int id)
    {
        Log.Info("SyncCustomerRepository -> Opening connection...");

        Thread.Sleep(200);

        Log.Info(
            $"SyncCustomerRepository -> Executing query for Customer #{id}...");

        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Customer> customers =
            _store.ReadAll<Customer>(DataFiles.Customers);

        Customer? customer = customers.FirstOrDefault(c => c.Id == id);

        if (customer is null)
        {
            Log.Error($"Customer #{id} not found.");
            return null;
        }

        Log.Success($"Customer #{id} found ({customer.Name})");

        return customer;
    }
}
