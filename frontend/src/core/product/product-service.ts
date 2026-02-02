import { inject, Injectable } from "@angular/core";
import { HttpClient, HttpErrorResponse, HttpStatusCode } from "@angular/common/http";
import { API_BASE_URL } from "../const";
import { firstValueFrom } from "rxjs";
import { z } from "zod";

@Injectable({
  providedIn: "root"
})
export class ProductService {
  private readonly baseUrl = `${API_BASE_URL}/products`;
  private readonly client: HttpClient = inject(HttpClient);

  public async getAllProducts(): Promise<IProduct[]> {
    // HttpClient will have the bearer token set automatically by the interceptor for every request!
    try {
      const response = await firstValueFrom(this.client.get(this.baseUrl, { observe: "response" }));
      return ProductsWire.parse(response.body) as IProduct[];
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        if (error.status === HttpStatusCode.Unauthorized) {
          console.log("Unauthenticated user cannot get products");
        } else {
          console.error("An error occurred while fetching products:", error.status);
        }
        return [];
      }
      throw error;
    }
  }

  public async getProductById(id: number): Promise<IProduct | null> {
    const url = `${this.baseUrl}/${id}`;
    try {
      const response = await firstValueFrom(this.client.get(url, { observe: "response" }));
      return ProductWire.parse(response.body) as IProduct;
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        if (error.status === HttpStatusCode.Unauthorized) {
          console.log("Unauthenticated user cannot get product by id");
        } else if (error.status === HttpStatusCode.NotFound) {
          console.log("Product not found");
        } else {
          console.error("An error occurred while fetching product by id:", error.status);
        }
        return null;
      }
      throw error;
    }
  }

  public async updatePrice(id: number, price: number): Promise<boolean> {
    const url = `${this.baseUrl}/${id}/price`;
    try {
      await firstValueFrom(this.client.patch(url, { price }, { observe: "response" }));
      return true;
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        switch (error.status) {
          case HttpStatusCode.Unauthorized: {
            console.log("Unauthenticated user cannot update price");
            break;
          }
          case HttpStatusCode.Forbidden: {
            console.log("User does not have permission to update price");
            break;
          }
          case HttpStatusCode.NotFound: {
            console.log("Product not found");
            break;
          }
          default: {
            console.error("An error occurred while updating product price:", error.status);
            break;
          }
        }
        return false;
      }
      throw error;
    }
  }
}

export interface IProduct {
  id: number;
  name: string;
  price: number;
}

const ProductWire = z.object({
  id: z.int(),
  name: z.string().min(1),
  price: z.int()
});
const ProductsWire = z.array(ProductWire);
