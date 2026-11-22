using FluentValidation;
using JwtDemo.Core;
using JwtDemo.Core.Auth;
using JwtDemo.Core.Products;
using JwtDemo.Util;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JwtDemo.Endpoints;

/// <summary>
///     The endpoints of the product resource
/// </summary>
public static class ProductEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapProductEndpoints()
        {
            var products = app.MapGroup("/api/products")
                              .WithTags("Products")
                              // every endpoint of the group requires (at least) the User role
                              .RequireAuthorization(nameof(UserRole.User));

            products.MapGet("/", GetAllProductsAsync);
            products.MapGet("/{productId:int}", GetProductByIdAsync);
            products.MapPatch("/{productId:int}/price", UpdatePriceAsync)
                    // Policies add up: the user needs the User role (group) AND the Admin role (endpoint).
                    // A valid token without the Admin role results in 403 Forbidden, no (valid) token in 401.
                    .RequireAuthorization(nameof(UserRole.Admin));
        }
    }

    private static async Task<Ok<IEnumerable<ProductDto>>> GetAllProductsAsync(
        [FromServices] IProductService productService)
    {
        IReadOnlyCollection<Product> products = await productService.GetAllProducts();

        return TypedResults.Ok(products.Select(ProductDto.FromProduct));
    }

    private static async Task<Results<Ok<ProductDto>, NotFound>> GetProductByIdAsync(
        [FromRoute] int productId, [FromServices] IProductService productService)
    {
        GetProductResult productResult = await productService.GetProductById(productId);

        return productResult switch
        {
            Product product => TypedResults.Ok(ProductDto.FromProduct(product)),
            ProductNotFound => TypedResults.NotFound()
        };
    }

    private static async Task<Results<NoContent, ValidationProblem, NotFound>> UpdatePriceAsync(
        [FromRoute] int productId,
        [FromBody] ProductPriceUpdateRequest request,
        [FromServices] IProductService productService,
        [FromServices] ITransactionProvider transaction)
    {
        if (new ProductPriceUpdateRequest.Validator().Check(request) is { } validationProblem)
        {
            return validationProblem;
        }

        await transaction.BeginTransactionAsync();

        PriceUpdateResult updateResult = await productService.UpdateProductPrice(productId, request.Price);

        return updateResult switch
        {
            Success => await transaction.CommitAndReturnAsync(TypedResults.NoContent()),
            ProductNotFound => TypedResults.NotFound()
        };
    }
}

public sealed class ProductPriceUpdateRequest
{
    public const decimal MaxPrice = 100_000M;

    // 'required': a request without a price is rejected, instead of silently setting the price to 0
    public required decimal Price { get; init; }

    public sealed class Validator : AbstractValidator<ProductPriceUpdateRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Price).InclusiveBetween(0, MaxPrice);
        }
    }
}

public sealed class ProductDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }

    public static ProductDto FromProduct(Product product) =>
        new()
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price
        };
}
