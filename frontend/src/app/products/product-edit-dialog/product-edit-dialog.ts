import { Component, inject, signal } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle
} from '@angular/material/dialog';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatButton } from '@angular/material/button';
import { MatInput } from '@angular/material/input';
import { form, FormField, FormRoot, max, min, required } from '@angular/forms/signals';
import { IProduct } from '../../../core/product/product-service';

@Component({
  selector: 'app-product-edit-dialog',
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatDialogClose,
    MatFormField,
    MatLabel,
    MatError,
    MatButton,
    MatInput,
    FormField,
    FormRoot
  ],
  templateUrl: './product-edit-dialog.html'
})
export class ProductEditDialog {
  private readonly dialogRef = inject<MatDialogRef<ProductEditDialog, number>>(MatDialogRef);
  protected readonly product = inject<IProduct>(MAT_DIALOG_DATA);

  private readonly priceModel = signal<{ price: number | null }>({ price: this.product.price });
  protected readonly priceForm = form(this.priceModel, path => {
    // same rules as in the backend - the backend has to validate anyway, this is just for a better user experience
    required(path.price, { message: 'Price is required' });
    min(path.price, 0, { message: 'Price must not be negative' });
    max(path.price, 100_000, { message: 'Price must not exceed 100,000' });
  }, {
    submission: {
      action: async form => {
        const price = form.price().value();
        if (price !== null) {
          this.dialogRef.close(price);
        }
        return undefined;
      }
    }
  });
}
