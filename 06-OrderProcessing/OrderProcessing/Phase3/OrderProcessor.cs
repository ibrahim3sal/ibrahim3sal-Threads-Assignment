
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Phase2;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEmailService _emailService;
    private readonly OrderRepository _orderRepository;

    public OrderProcessor(
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        IEmailService emailService,
        OrderRepository orderRepository)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _emailService = emailService;
        _orderRepository = orderRepository;
    }

    public async Task<bool> ProcessOrderAsync(
        Order order,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Log.Info($"Starting Order #{order.Id}");

        bool stockReserved = false;
        bool paymentCaptured = false;

        try
        {
            // 1. Get customer
            Customer customer;

            try
            {
                customer = await _customerService.GetCustomerAsync(
                    order.CustomerId, ct);
            }
            catch (CustomerNotFoundException ex)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            if (!customer.IsActive)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";
                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            // 2. Reserve stock
            stockReserved = await _inventoryService.ReserveStockAsync(order, ct);

            if (!stockReserved)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Insufficient stock";
                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            // 3. Calculate total
            try
            {
                await _pricingService.CalculateTotalAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                await _inventoryService.ReleaseStockAsync(
                    order, CancellationToken.None);

                stockReserved = false;
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            // 4. Process payment with 3-second timeout
            using var paymentCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);

            paymentCts.CancelAfter(TimeSpan.FromSeconds(3));

            PaymentResult paymentResult;

            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(
                    order.Id,
                    order.TotalAmount,
                    paymentCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await _inventoryService.ReleaseStockAsync(
                    order, CancellationToken.None);

                stockReserved = false;
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout";

                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            if (ct.IsCancellationRequested)
                ct.ThrowIfCancellationRequested();

            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseStockAsync(
                    order, CancellationToken.None);

                stockReserved = false;
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message;

                await _orderRepository.SaveAsync(order, CancellationToken.None);
                return false;
            }

            paymentCaptured = true;
            order.TransactionId = paymentResult.TransactionId;

            // 5. Save completed order
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                await _orderRepository.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException)
            {
                await _paymentGateway.RefundAsync(
                    order.TransactionId!,
                    CancellationToken.None);

                await _inventoryService.ReleaseStockAsync(
                    order, CancellationToken.None);

                paymentCaptured = false;
                stockReserved = false;

                order.Status = OrderStatus.Failed;
                order.FailureReason =
                    $"Database save failed after payment; payment {order.TransactionId} refunded";

                await _orderRepository.SaveAsync(
                    order, CancellationToken.None);

                return false;
            }

            // 6. Send confirmation email
            await SendEmailWithRetryAsync(order, customer, ct);

            Log.Success($"Order #{order.Id} completed successfully");

            return true;
        }
        catch (OperationCanceledException)
        {
            if (stockReserved && !paymentCaptured)
            {
                await _inventoryService.ReleaseStockAsync(
                    order, CancellationToken.None);
            }

            throw;
        }
        finally
        {
            stopwatch.Stop();

            Log.Info(
                $"Order #{order.Id} processing duration: {stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private async Task SendEmailWithRetryAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        int[] delays = { 500, 1000, 2000 };

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await _emailService.SendConfirmationAsync(
                    order, customer, ct);

                return;
            }
            catch (SmtpUnavailableException ex)
            {
                Log.Error(
                    $"Email attempt {attempt} failed for Order #{order.Id}: {ex.Message}");

                if (attempt == 4)
                {
                    await _emailService.SaveFailedEmailAsync(
                        order,
                        customer,
                        attempt,
                        ex.Message,
                        CancellationToken.None);

                    return;
                }

                await Task.Delay(delays[attempt - 1], ct);
            }
        }
    }
}
