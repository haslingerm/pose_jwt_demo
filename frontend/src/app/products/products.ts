import { Component, computed, inject, signal } from '@angular/core';
import { MatCard } from '@angular/material/card';
import {
  MatCell,
  MatCellDef,
  MatColumnDef,
  MatHeaderCell,
  MatHeaderCellDef,
  MatHeaderRow,
  MatHeaderRowDef,
  MatRow,
  MatRowDef,
  MatTable
} from '@angular/material/table';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CurrencyPipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { IProduct, ProductService } from '../../core/product/product-service';
import { UserRole, UserService } from '../../core/user/user-service';
import { ProductEditDialog } from './product-edit-dialog/product-edit-dialog';

@Component({
  selector: 'app-products',
  imports: [
    MatCard,
    MatTable,
    MatColumnDef,
    MatHeaderCell,
    MatHeaderCellDef,
    MatCell,
    MatCellDef,
    MatHeaderRow,
    MatHeaderRowDef,
    MatRow,
    MatRowDef,
    MatIconButton,
    MatIcon,
    MatProgressBar,
    CurrencyPipe
  ],
  templateUrl: './products.html',
  styleUrl: './products.scss'
})
export class Products {
  private static readonly baseColumns = ['id', 'name', 'price'];
  private readonly productService = inject(ProductService);
  private readonly userService = inject(UserService);
  private readonly dialog = inject(MatDialog);
  private readonly snackbar = inject(MatSnackBar);

  // loads the products as soon as the component is created
  protected readonly products = this.productService.createProductsResource();
  private readonly updating = signal(false);
  protected readonly loading = computed(() => this.products.isLoading() || this.updating());

  // only for the UI - the backend checks the role again when the price is updated
  protected readonly isAdmin = computed(() => this.userService.hasRole(UserRole.Admin));
  protected readonly displayedColumns = computed(() => this.isAdmin()
    ? [...Products.baseColumns, 'edit']
    : Products.baseColumns);

  protected async editProduct(productId: number): Promise<void> {
    // we could (and should) get this from the loaded products, just wanted to use all API endpoints
    const product = await this.productService.getProductById(productId);
    if (!product) {
      return;
    }

    const dialogRef = this.dialog.open<ProductEditDialog, IProduct, number>(ProductEditDialog, {
      data: product
    });
    const newPrice = await firstValueFrom(dialogRef.afterClosed());

    // undefined = dialog was cancelled (button, escape key or click outside the dialog)
    if (newPrice === undefined || newPrice === product.price) {
      return;
    }

    this.updating.set(true);
    const success = await this.productService.updatePrice(productId, newPrice);
    this.updating.set(false);

    if (!success) {
      this.snackbar.open('Updating the price failed', 'Close', { duration: 4000 });
    }

    // a local update instead of a full reload would be more efficient
    this.products.reload();
  }
}
