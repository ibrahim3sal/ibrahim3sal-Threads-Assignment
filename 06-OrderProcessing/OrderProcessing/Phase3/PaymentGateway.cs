
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;


namespace OrderProcessing.Phase3;

public class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    public PaymentGateway(
        JsonFileStore store,
        IEnumerable<int>? timeoutOrderIds = null,
        IEnumerable<int>? declineOrderIds = null,
        bool enableFailures = true)
    {
        _store = store;

        _timeoutOrderIds = enableFailures
            ? timeoutOrderIds?.ToHashSet() ?? new HashSet<int>()
            : new HashSet<int>();

        _declineOrderIds = enableFailures
            ? declineOrderIds?.ToHashSet() ?? new HashSet<int>()
            : new HashSet<int>();
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken ct = default)
    {
        Log.Info($"PaymentGateway -> Processing payment for Order #{orderId}");

        int delay = _timeoutOrderIds.Contains(orderId)
            ? 5000
            : 700;

        Log.Info($"PaymentGateway -> Waiting for payment response...");

        await Task.Delay(delay, ct);

        // Simulate fraud check
        Log.Info($"PaymentGateway -> Running fraud check for Order #{orderId}");

        await Task.Delay(100, ct);

        bool declined = _declineOrderIds.Contains(orderId);

        string? transactionId = declined
            ? null
            : "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        PaymentStatus status = declined
            ? PaymentStatus.Declined
            : PaymentStatus.Captured;

        string? message = declined
            ? "Card declined by issuer"
            : "Payment captured successfully";

        var transaction = new PaymentTransaction
        {
            OrderId = orderId,
            Amount = amount,
            Status = status,
            TransactionId = transactionId,
            CreatedAt = DateTime.Now
        };

        await _store.UpdateAsync<PaymentTransaction>(
            DataFiles.Payments,
            payments =>
            {
                payments.Add(transaction);
                return payments;
            },
            ct);

        Log.Info(
            $"PaymentGateway -> Order #{orderId}: {status}");

        return new PaymentResult
        {
            Success = !declined,
            TransactionId = transactionId,
            Status = status,
            Message = message
        };
    }

    public async Task RefundAsync(
        string transactionId,
        CancellationToken ct = default)
    {
        Log.Info($"PaymentGateway -> Refunding transaction {transactionId}");

        await _store.UpdateAsync<PaymentTransaction>(
            DataFiles.Payments,
            payments =>
            {
                var transaction = payments.FirstOrDefault(
                    p => p.TransactionId == transactionId);

                if (transaction is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{transactionId}' not found.");
                }

                transaction.Status = PaymentStatus.Refunded;

                return payments;
            },
            ct);

        Log.Success(
            $"PaymentGateway -> Transaction {transactionId} refunded");
    }
}
