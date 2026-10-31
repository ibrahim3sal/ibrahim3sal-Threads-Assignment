
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Shared.Repositories;

public class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Customer?> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        Log.Info("CustomerRepository -> Opening connection...");

        await Task.Delay(200, ct);

        Log.Info(
            $"CustomerRepository -> Executing query for Customer #{id}...");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Customer> customers =
            await _store.ReadAllAsync<Customer>(DataFiles.Customers, ct);

        Customer? customer = customers.FirstOrDefault(c => c.Id == id);

        if (customer is null)
        {
            Log.Error($"Customer #{id} not found.");
            return null;
        }

        Log.Success(
            $"Customer #{id} found ({customer.Name})");

        return customer;
    }
}
