using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;

namespace OrderProcessing.Phase3;

public class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(
        OrderRepository orderRepository,
        IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    public async Task ProcessOrderAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order, CancellationToken.None);
// #pragma warning disable CS4014
//         _emailService.SendConfirmationAsync(order, customer);
// #pragma warning restore CS4014
        await _emailService.SendConfirmationAsync(order, customer);

        Console.WriteLine("Order processing finished");
    }
}