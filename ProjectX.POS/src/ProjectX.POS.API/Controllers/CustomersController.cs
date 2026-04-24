using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.POS.API.Contracts.Customers;
using ProjectX.POS.Application.Customers;
using ProjectX.POS.Application.Products;

namespace ProjectX.POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController(ICustomersService customersService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerModel>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await customersService.GetCustomersAsync(search, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customersService.GetCustomerByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerModel>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customersService.CreateCustomerAsync(
            new CreateCustomerInput(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.Notes,
                request.MarketingOptIn,
                request.TaxExempt),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerModel>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customersService.UpdateCustomerAsync(
            id,
            new UpdateCustomerInput(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.Notes,
                request.MarketingOptIn,
                request.TaxExempt),
            cancellationToken);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await customersService.DeleteCustomerAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
