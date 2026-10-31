using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncOrderRepository
{
    private readonly JsonFileStore _store;

    public SyncOrderRepository(JsonFileStore store)
    {
        _store = store;
    }

    public void Save(Order order)
    {
        _store.Update<Order>(
            DataFiles.Orders,
            orders =>
            {
                var existing = orders.FirstOrDefault(
                    o => o.Id == order.Id);

                if (existing is null)
                    orders.Add(order);
                else
                {
                    var index = orders.IndexOf(existing);
                    orders[index] = order;
                }

                return orders;
            });
    }
}