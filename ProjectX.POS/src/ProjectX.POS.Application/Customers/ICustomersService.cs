using ProjectX.POS.Application.Products;

namespace ProjectX.POS.Application.Customers;

public interface ICustomersService
{
    Task<PagedResult<CustomerModel>> GetCustomersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<CustomerModel?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerModel> CreateCustomerAsync(CreateCustomerInput input, CancellationToken cancellationToken);

    Task<CustomerModel?> UpdateCustomerAsync(Guid id, UpdateCustomerInput input, CancellationToken cancellationToken);

    Task<bool> DeleteCustomerAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CreateCustomerInput(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes,
    bool MarketingOptIn,
    bool TaxExempt);

public sealed record UpdateCustomerInput(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes,
    bool MarketingOptIn,
    bool TaxExempt);

public sealed record CustomerModel(
    Guid Id,
    Guid ProjectId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Phone,
    string Notes,
    bool MarketingOptIn,
    bool TaxExempt,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
