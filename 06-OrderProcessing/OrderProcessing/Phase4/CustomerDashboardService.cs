
using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Storage;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Phase4;

public class CustomerDashboardService
{
    private readonly CustomerRepository _customerRepository;
    private readonly OrderRepository _orderRepository;
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(
        CustomerRepository customerRepository,
        OrderRepository orderRepository,
        JsonFileStore store,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _store = store;

        _failPaymentsForCustomerIds =
            failPaymentsForCustomerIds?.ToHashSet()
            ?? new HashSet<int>();
    }

    public async Task<CustomerDashboard> LoadAsync(
        int customerId,
        CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Start all four loads concurrently.
        Task<Customer> customerTask =
            LoadCustomerAsync(customerId, ct);

        Task<List<Order>> ordersTask =
            LoadOrdersAsync(customerId, ct);

        Task<List<PaymentTransaction>> paymentsTask =
            LoadPaymentsAsync(customerId, ct);

        Task<List<EmailMessage>> emailsTask =
            LoadEmailsAsync(ct);

        // Wait for all tasks with one await.
        Task allTasks = Task.WhenAll(
            customerTask,
            ordersTask,
            paymentsTask,
            emailsTask);

        try
        {
            await allTasks;
        }
        catch
        {
            if (allTasks.Exception is not null)
            {
                throw allTasks.Exception.Flatten();
            }

            throw;
        }

        // All tasks succeeded. Read their results.
        Customer customer = customerTask.Result;
        List<Order> orders = ordersTask.Result;

        HashSet<int> orderIds = orders
            .Select(o => o.Id)
            .ToHashSet();

        List<PaymentTransaction> payments = paymentsTask.Result
            .Where(p => orderIds.Contains(p.OrderId))
            .ToList();

        List<EmailMessage> emails = emailsTask.Result
            .Where(e => orderIds.Contains(e.OrderId))
            .ToList();

        stopwatch.Stop();

        Console.WriteLine(
            $"Dashboard loaded in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

        return new CustomerDashboard
        {
            Customer = customer,
            Orders = orders,
            Payments = payments,
            Emails = emails
        };
    }

    private async Task<Customer> LoadCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        Customer? customer =
            await _customerRepository.GetByIdAsync(customerId, ct);

        if (customer is null)
        {
            throw new CustomerNotFoundException(
                $"Customer #{customerId} not found");
        }

        return customer;
    }

    private async Task<List<Order>> LoadOrdersAsync(
        int customerId,
        CancellationToken ct)
    {
        List<Order> allOrders =
            await _orderRepository.GetAllAsync(ct);

        return allOrders
            .Where(o => o.CustomerId == customerId)
            .ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(
        int customerId,
        CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException(
                "Payments store timeout");
        }

        return await _store.ReadAllAsync<PaymentTransaction>(
            DataFiles.Payments, ct);
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(
        CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadAllAsync<EmailMessage>(
            DataFiles.Emails, ct);
    }
}
