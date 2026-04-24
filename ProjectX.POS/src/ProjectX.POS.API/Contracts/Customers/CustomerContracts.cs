namespace ProjectX.POS.API.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes,
    bool MarketingOptIn,
    bool TaxExempt);

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes,
    bool MarketingOptIn,
    bool TaxExempt);
