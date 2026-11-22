namespace JwtDemo.Core.Products;

public readonly record struct ProductNotFound;

public union GetProductResult(Product, ProductNotFound);

public union PriceUpdateResult(Success, ProductNotFound);

public interface IProductService
{
    public ValueTask<IReadOnlyCollection<Product>> GetAllProducts();
    public ValueTask<GetProductResult> GetProductById(int productId);
    public ValueTask<PriceUpdateResult> UpdateProductPrice(int productId, decimal newPrice);
}

// not really relevant for this demo - basic implementation for completeness
internal sealed class ProductService(IUnitOfWork unitOfWork) : IProductService
{
    public ValueTask<IReadOnlyCollection<Product>> GetAllProducts() => unitOfWork.ProductRepository.GetAllProducts();

    public async ValueTask<GetProductResult> GetProductById(int productId) =>
        await unitOfWork.ProductRepository.GetById(productId) is { } product
            ? product
            : new ProductNotFound();

    public async ValueTask<PriceUpdateResult> UpdateProductPrice(int productId, decimal newPrice)
    {
        if (await unitOfWork.ProductRepository.GetById(productId) is not { } product)
        {
            return new ProductNotFound();
        }

        product.Price = newPrice;

        return new Success();
    }
}
