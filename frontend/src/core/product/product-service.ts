import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpResourceRef, httpResource, HttpStatusCode } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { z } from 'zod';
import { API_BASE_URL } from '../const';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private readonly baseUrl = `${API_BASE_URL}/products`;
  private readonly client = inject(HttpClient);

  /**
   * Creates a signal based resource of all products (value, isLoading, error, reload, ...).
   * Has to be called in an injection context, e.g. a field initializer of a component.
   * The interceptor adds the bearer token automatically, as for every HttpClient request.
   */
  public createProductsResource(): HttpResourceRef<IProduct[]> {
    return httpResource(() => this.baseUrl, {
      parse: data => ProductsWire.parse(data),
      defaultValue: []
    });
  }

  public async getProductById(id: number): Promise<IProduct | null> {
    try {
      const response = await firstValueFrom(this.client.get(`${this.baseUrl}/${id}`));
      return ProductWire.parse(response);
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        console.error(`Failed to get product ${id}: ${error.status}`);
        return null;
      }
      throw error;
    }
  }

  public async updatePrice(id: number, price: number): Promise<boolean> {
    try {
      await firstValueFrom(this.client.patch(`${this.baseUrl}/${id}/price`, { price }));
      return true;
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) {
        throw error;
      }

      switch (error.status) {
        case HttpStatusCode.Unauthorized:
          console.log('Unauthenticated user cannot update price');
          break;
        case HttpStatusCode.Forbidden:
          console.log('User does not have permission to update price');
          break;
        case HttpStatusCode.NotFound:
          console.log('Product not found');
          break;
        default:
          console.error('An error occurred while updating product price:', error.status);
          break;
      }
      return false;
    }
  }
}

const ProductWire = z.object({
  id: z.number(),
  name: z.string().min(1),
  price: z.number()
});
const ProductsWire = z.array(ProductWire);

export type IProduct = z.infer<typeof ProductWire>;
