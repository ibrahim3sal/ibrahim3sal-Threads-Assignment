
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public interface IEmailService
{
    Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken ct = default);

    Task SaveFailedEmailAsync(
        Order order,
        Customer customer,
        int attempts,
        string lastError,
        CancellationToken ct = default);
}
