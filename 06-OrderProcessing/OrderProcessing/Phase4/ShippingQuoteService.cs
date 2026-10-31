using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase4.Shipping;

public class ShippingQuoteService
{
    private const int TimeBudgetMs = 1200;

    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers.ToList();
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        using CancellationTokenSource cts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        List<(IShippingCarrier Carrier, Task<decimal> Task)> running =
            _carriers
                .Select(carrier => (
                    Carrier: carrier,
                    Task: carrier.GetQuoteAsync(order, cts.Token)))
                .ToList();

        Task timeoutTask = Task.Delay(TimeBudgetMs, ct);

        while (running.Count > 0)
        {
            Task completed = await Task.WhenAny(
                running.Select(x => (Task)x.Task)
                    .Append(timeoutTask));

            // The caller cancelled the operation.
            if (ct.IsCancellationRequested)
            {
                cts.Cancel();
                await ObserveAllTasksAsync(running);
                ct.ThrowIfCancellationRequested();
            }

            // The 1.2-second time budget expired.
            if (completed == timeoutTask)
            {
                var successful = running.FirstOrDefault(
                    x => x.Task.IsCompletedSuccessfully);

                if (successful.Task is not null)
                {
                    long elapsed = stopwatch.ElapsedMilliseconds;

                    cts.Cancel();
                    await ObserveAllTasksAsync(running);

                    return new ShippingQuote
                    {
                        Carrier = successful.Carrier.Name,
                        Price = successful.Task.Result,
                        ElapsedMs = elapsed
                    };
                }

                foreach (var item in running
                    .Where(x => x.Task.IsCompleted)
                    .ToList())
                {
                    running.Remove(item);
                    await ObserveTaskAsync(item.Task);
                }

                if (running.Count == 0)
                {
                    throw new ShippingProviderUnavailableException(
                        "All carriers failed");
                }

                long timeoutElapsed = stopwatch.ElapsedMilliseconds;

                cts.Cancel();
                await ObserveAllTasksAsync(running);

                return new ShippingQuote
                {
                    Carrier = "Flat rate",
                    Price = 25.00m,
                    ElapsedMs = timeoutElapsed
                };
            }

            var completedCarrier = running.First(
                x => x.Task == completed);

            running.Remove(completedCarrier);

            try
            {
                decimal price = await completedCarrier.Task;
                long elapsed = stopwatch.ElapsedMilliseconds;

                // A successful quote has won.
                cts.Cancel();
                await ObserveAllTasksAsync(running);

                return new ShippingQuote
                {
                    Carrier = completedCarrier.Carrier.Name,
                    Price = price,
                    ElapsedMs = elapsed
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                cts.Cancel();
                await ObserveAllTasksAsync(running);
                throw;
            }
            catch
            {
                // This carrier failed. Wait for the remaining carriers.
            }
        }

        throw new ShippingProviderUnavailableException(
            "All carriers failed");
    }

    private static async Task ObserveAllTasksAsync(
        List<(IShippingCarrier Carrier, Task<decimal> Task)> tasks)
    {
        foreach (var item in tasks)
        {
            await ObserveTaskAsync(item.Task);
        }
    }

    private static async Task ObserveTaskAsync(Task<decimal> task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Observe failures and cancellations.
        }
    }
}