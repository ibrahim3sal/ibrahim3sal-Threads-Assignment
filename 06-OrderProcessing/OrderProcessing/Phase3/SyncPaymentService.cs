using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncPaymentService
{
    private readonly JsonFileStore _store;

    public SyncPaymentService(JsonFileStore store)
    {
        _store = store;
    }

    public string ProcessPayment(Order order)
    {
        // Simulate payment processing delay
        Thread.Sleep(700);

        string transactionId = Guid.NewGuid().ToString();

        var transaction = new PaymentTransaction
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Captured,
            TransactionId = transactionId,
            CreatedAt = DateTime.UtcNow
        };

        _store.Update<PaymentTransaction>(
            DataFiles.Payments,
            payments =>
            {
                payments.Add(transaction);
                return payments;
            });

        return transactionId;
    }
}