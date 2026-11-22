using System.ComponentModel.DataAnnotations;
using JwtDemo.Core.Products;

namespace JwtDemo.Model;

public sealed class ProductDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
}

public sealed class ProductPriceUpdateRequest
{
    // 'required': a request without a price is rejected, instead of silently setting the price to 0
    [Range(typeof(decimal), "0", "100000")]
    public required decimal Price { get; init; }
}

public static class ProductMappings
{
    extension(Product product)
    {
        public ProductDto ToDto() =>
            new()
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price
            };
    }
}
