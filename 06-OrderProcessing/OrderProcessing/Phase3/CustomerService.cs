using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Repositories;

namespace OrderProcessing.Phase3;

public class CustomerService
{
    private readonly CustomerRepository _customerRepository;

    public CustomerService(CustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Customer> GetCustomerAsync(
        int id,
        CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, ct);

        if (customer is null)
        {
            throw new CustomerNotFoundException(
                $"Customer #{id} not found");
        }

        return customer;
    }
}