using JwtDemo.Core;
using JwtDemo.Core.Auth;
using JwtDemo.Core.Products;
using JwtDemo.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JwtDemo.Controllers;

[ApiController]
[Authorize(nameof(UserRole.User))]
[Route("api/products")]
public sealed class ProductController(IProductService productService, ITransactionProvider transaction) : ControllerBase
{
    [HttpGet]
    public async ValueTask<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        var products = await productService.GetAllProducts();

        return Ok(products.Select(p => p.ToDto()));
    }

    [HttpGet("{productId:int}")]
    public async ValueTask<ActionResult<ProductDto>> GetById([FromRoute] int productId) =>
        await productService.GetProductById(productId) switch
        {
            Product product => Ok(product.ToDto()),
            ProductNotFound => NotFound()
        };

    // policies are combined: the user needs to be authorized by the controller ([Authorize(User)])
    // AND by this method ([Authorize(Admin)]) - a token without the Admin role results in 403 Forbidden
    [HttpPatch("{productId:int}/price")]
    [Authorize(nameof(UserRole.Admin))]
    public async ValueTask<IActionResult> UpdatePrice([FromRoute] int productId,
                                                      [FromBody] ProductPriceUpdateRequest request)
    {
        await transaction.BeginTransactionAsync();

        var updateResult = await productService.UpdateProductPrice(productId, request.Price);

        return updateResult switch
        {
            Success => await transaction.CommitAndReturn(NoContent()),
            ProductNotFound => NotFound()
        };
    }
}
