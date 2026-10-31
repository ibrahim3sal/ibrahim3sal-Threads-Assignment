
using System.Collections.Concurrent;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase3;

public class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly ConcurrentDictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attemptCounts = new();
    private readonly int _sendDelayMs;

    public EmailService(
        JsonFileStore store,
        Dictionary<int, int>? failAttempts = null,
        bool enableFailures = true,
        int sendDelayMs = 300
        )
    {
        _store = store;
        _sendDelayMs = sendDelayMs;

        _failAttempts = new ConcurrentDictionary<int, int>(
            enableFailures
                ? failAttempts ?? new Dictionary<int, int>()
                : new Dictionary<int, int>());
    }

    public async Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken ct = default)
    {
        int attempt = _attemptCounts.AddOrUpdate(
            order.Id, 1, (_, count) => count + 1);

        Log.Info(
            $"EmailService -> Sending Order #{order.Id}, attempt {attempt}");

        if (_failAttempts.TryGetValue(order.Id, out int failures)
            && failures > 0)
        {
            _failAttempts.TryUpdate(
                order.Id, failures - 1, failures);

            Log.Error(
                $"EmailService -> Attempt {attempt} failed for Order #{order.Id}");

            throw new SmtpUnavailableException(
                $"SMTP server unavailable for Order #{order.Id}");
        }

        await Task.Delay(_sendDelayMs, ct);

        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Your order #{order.Id} has been completed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempt
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.Emails,
            emails =>
            {
                emails.Add(email);
                return emails;
            },
            ct);

        Log.Success(
            $"EmailService -> Confirmation sent for Order #{order.Id}");
    }

    public async Task SaveFailedEmailAsync(
        Order order,
        Customer customer,
        int attempts,
        string lastError,
        CancellationToken ct = default)
    {
        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Your order #{order.Id} has been completed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempts,
            LastError = lastError
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.EmailRetry,
            emails =>
            {
                emails.Add(email);
                return emails;
            },
            ct);

        Log.Error(
            $"EmailService -> Order #{order.Id} added to email retry file");
    }
}
