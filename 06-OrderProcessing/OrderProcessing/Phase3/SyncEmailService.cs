using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncEmailService
{
    private readonly JsonFileStore _store;

    public SyncEmailService(JsonFileStore store)
    {
        _store = store;
    }

    public void SendConfirmation(Order order, Customer customer)
    {
        Thread.Sleep(300);

        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Your order #{order.Id} has been completed successfully.",
            SentAt = DateTime.UtcNow,
            Attempts = 1,
            LastError = null
        };

        _store.Update<EmailMessage>(
            DataFiles.Emails,
            emails =>
            {
                emails.Add(email);
                return emails;
            });
    }
}