
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Repositories;

public class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(
        JsonFileStore store,
        IEnumerable<int>? failSaveForOrderIds = null,
        bool enableFailures = true)
    {
        _store = store;

        _failSaveForOrderIds = enableFailures
            ? failSaveForOrderIds?.ToHashSet() ?? new HashSet<int>()
            : new HashSet<int>();
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"OrderRepository -> Saving Order #{order.Id}...");

        if (order.Status == OrderStatus.Completed &&
            _failSaveForOrderIds.Contains(order.Id))
        {
            throw new DatabaseWriteException(
                "Connection lost during commit");
        }

        await Task.Delay(400, ct);

        await _store.UpdateAsync<Order>(
            DataFiles.Orders,
            orders =>
            {
                int index = orders.FindIndex(o => o.Id == order.Id);

                if (index >= 0)
                    orders[index] = order;
                else
                    orders.Add(order);

                return orders;
            },
            ct);

        Log.Info("OrderRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        Log.Success($"Order #{order.Id} saved.");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing query for all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
    }

    public async Task<List<Order>> GetByStatusAsync(
        OrderStatus status,
        CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for {status} orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders =
            await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);

        return orders.Where(o => o.Status == status).ToList();
    }
}
