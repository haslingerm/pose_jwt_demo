import { Component, computed, inject, signal } from '@angular/core';
import { MatCard, MatCardActions, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatButton } from '@angular/material/button';
import { MatInput } from '@angular/material/input';
import { form, FormField, FormRoot, required, ValidationError } from '@angular/forms/signals';
import { AuthService, LoginResult } from '../../core/auth/auth-service';
import { UserRole, UserService } from '../../core/user/user-service';

@Component({
  selector: 'app-login',
  imports: [
    MatCard,
    MatCardHeader,
    MatCardContent,
    MatCardTitle,
    MatCardActions,
    MatError,
    MatFormField,
    MatLabel,
    MatButton,
    MatInput,
    FormField,
    FormRoot
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private static readonly emptyLoginModel: LoginFormModel = { username: '', password: '' };
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);

  protected readonly user = this.userService.user;
  protected readonly roleName = computed(() => {
    const user = this.user();
    return user ? UserRole[user.role] : null;
  });

  private readonly loginModel = signal<LoginFormModel>(Login.emptyLoginModel);
  protected readonly loginForm = form(this.loginModel, path => {
    required(path.username, { message: 'Username is required' });
    required(path.password, { message: 'Password is required' });
  }, {
    submission: {
      // called by [formRoot] when the form is submitted (button or enter key) - only if the form is valid
      action: async form => {
        const { username, password } = form().value();
        const result = await this.authService.login(username, password);
        if (result === 'success') {
          this.resetForm();
          return undefined;
        }

        // shown as an error of the whole form
        return Login.toError(result);
      }
    }
  });

  protected async logout(): Promise<void> {
    await this.authService.logout();
  }

  private resetForm(): void {
    this.loginModel.set(Login.emptyLoginModel);
    this.loginForm().reset();
  }

  private static toError(result: Exclude<LoginResult, 'success'>): ValidationError {
    switch (result) {
      case 'invalidCredentials':
        // don't tell the user whether the username or the password was wrong
        return { kind: 'invalidCredentials', message: 'Username or password is incorrect' };
      case 'tooManyAttempts':
        return { kind: 'tooManyAttempts', message: 'Too many attempts - please try again in a minute' };
      case 'error':
        return { kind: 'error', message: 'Login failed - please try again later' };
    }
  }
}

type LoginFormModel = {
  username: string;
  password: string;
}
