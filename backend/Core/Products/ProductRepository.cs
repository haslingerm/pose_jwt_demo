using Microsoft.EntityFrameworkCore;

namespace JwtDemo.Core.Products;

public interface IProductRepository
{
    public ValueTask<IReadOnlyCollection<Product>> GetAllProducts();
    public ValueTask<Product?> GetById(int productId);
}

public sealed class ProductRepository(DbSet<Product> products) : IProductRepository
{
    public async ValueTask<IReadOnlyCollection<Product>> GetAllProducts() =>
        await products.OrderBy(p => p.Id).ToListAsync();

    public ValueTask<Product?> GetById(int productId) => products.FindAsync(productId);
}
