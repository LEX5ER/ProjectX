using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectX.POS.API.Contracts.Products;
using ProjectX.POS.Application.Products;

namespace ProjectX.POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController(IProductsService productsService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<PagedResult<ProductModel>>> GetCatalog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await productsService.GetCatalogAsync(page, pageSize, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductModel>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await productsService.GetProductsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await productsService.GetProductByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductModel>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productsService.CreateProductAsync(
            request.Code,
            request.Name,
            request.Description,
            request.Category,
            request.UnitPrice,
            request.StockQuantity,
            request.Status,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductModel>> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productsService.UpdateProductAsync(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.Category,
            request.UnitPrice,
            request.StockQuantity,
            request.Status,
            cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await productsService.DeleteProductAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
