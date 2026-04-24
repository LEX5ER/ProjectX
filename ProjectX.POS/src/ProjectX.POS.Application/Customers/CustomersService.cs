using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;
using ProjectX.POS.Application.Products;
using ProjectX.POS.Domain.Entities;

namespace ProjectX.POS.Application.Customers;

public sealed class CustomersService(
    IApplicationDbContext dbContext,
    IRequestContextAccessor requestContextAccessor,
    IIamAuthorizationContextService authorizationContextService) : ICustomersService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<CustomerModel>> GetCustomersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanReadAny(accessContext, "customer records");

        var query = dbContext.Customers
            .AsNoTracking()
            .AsQueryable();

        if (!accessContext.CanReadAllProducts && accessContext.ActiveProjectId.HasValue)
        {
            query = query.Where(customer => customer.ProjectId == accessContext.ActiveProjectId.Value);
        }

        query = ApplySearch(query, search);

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<CustomerModel?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(currentCustomer => currentCustomer.Id == id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanReadProject(accessContext, customer.ProjectId, "customer records");

        return Map(customer);
    }

    public async Task<CustomerModel> CreateCustomerAsync(CreateCustomerInput input, CancellationToken cancellationToken)
    {
        ValidateCustomer(input.FirstName, input.LastName, input.Email, input.Phone, input.Notes);

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        var projectId = PosAuthorizationGuards.RequireActiveProjectForManagement(accessContext, "customer records");
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Email = NormalizeOptional(input.Email),
            Phone = NormalizeOptional(input.Phone),
            Notes = NormalizeOptional(input.Notes),
            MarketingOptIn = input.MarketingOptIn,
            TaxExempt = input.TaxExempt,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<CustomerModel?> UpdateCustomerAsync(Guid id, UpdateCustomerInput input, CancellationToken cancellationToken)
    {
        ValidateCustomer(input.FirstName, input.LastName, input.Email, input.Phone, input.Notes);

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(currentCustomer => currentCustomer.Id == id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanManageProject(accessContext, customer.ProjectId, "customer records");

        customer.FirstName = input.FirstName.Trim();
        customer.LastName = input.LastName.Trim();
        customer.Email = NormalizeOptional(input.Email);
        customer.Phone = NormalizeOptional(input.Phone);
        customer.Notes = NormalizeOptional(input.Notes);
        customer.MarketingOptIn = input.MarketingOptIn;
        customer.TaxExempt = input.TaxExempt;
        customer.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<bool> DeleteCustomerAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(currentCustomer => currentCustomer.Id == id, cancellationToken);

        if (customer is null)
        {
            return false;
        }

        var accessContext = await PosAuthorizationGuards.GetAuthorizationContextAsync(
            requestContextAccessor,
            authorizationContextService,
            cancellationToken);
        PosAuthorizationGuards.EnsureCanManageProject(accessContext, customer.ProjectId, "customer records");

        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static IQueryable<Customer> ApplySearch(IQueryable<Customer> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim();

        return query.Where(customer =>
            customer.FirstName.Contains(normalizedSearch)
            || customer.LastName.Contains(normalizedSearch)
            || customer.Email.Contains(normalizedSearch)
            || customer.Phone.Contains(normalizedSearch)
            || customer.Notes.Contains(normalizedSearch));
    }

    private static void ValidateCustomer(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        string? notes)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            errors["fullName"] = ["A customer must have at least a first name or last name."];
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                _ = new MailAddress(email.Trim());
            }
            catch (FormatException)
            {
                errors["email"] = ["Email must be a valid email address."];
            }
        }

        if ((notes?.Trim().Length ?? 0) > 500)
        {
            errors["notes"] = ["Notes must be 500 characters or fewer."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static CustomerModel Map(Customer customer)
    {
        return new CustomerModel(
            customer.Id,
            customer.ProjectId,
            customer.FirstName,
            customer.LastName,
            BuildFullName(customer.FirstName, customer.LastName),
            customer.Email,
            customer.Phone,
            customer.Notes,
            customer.MarketingOptIn,
            customer.TaxExempt,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }

    private static string BuildFullName(string firstName, string lastName)
    {
        return string.Join(" ", new[] { firstName.Trim(), lastName.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string NormalizeOptional(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static async Task<PagedResult<CustomerModel>> CreatePagedResultAsync(
        IQueryable<Customer> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var customers = await query
            .OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName)
            .ThenBy(customer => customer.Email)
            .Skip(skip)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);
        var items = customers
            .Select(Map)
            .ToList();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new PagedResult<CustomerModel>(items, normalizedPage, normalizedPageSize, totalCount, totalPages);
    }
}
