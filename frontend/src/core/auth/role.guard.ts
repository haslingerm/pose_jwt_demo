import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { UserRole, UserService } from '../user/user-service';
import { AuthService } from './auth-service';

/**
 * Keep in mind: guards only improve the user experience - everything in the browser can be manipulated.
 * The real protection is the backend, which checks the token (and its roles) on every request.
 */
export function roleGuard(requiredRole: UserRole): CanActivateFn {
  return async () => {
    // we have to inject all dependencies before awaiting anything
    const authService = inject(AuthService);
    const userService = inject(UserService);
    const router = inject(Router);
    const snackbar = inject(MatSnackBar);

    // makes sure the login is still valid (refreshes the token, or logs out if that is not possible anymore)
    await authService.getAccessToken();

    if (userService.hasRole(requiredRole)) {
      return true;
    }

    snackbar.open(`Only users with role '${UserRole[requiredRole]}' can access this page`, 'Close', {
      duration: 4000,
      verticalPosition: 'top',
    });

    return userService.isLoggedIn() ? false : router.createUrlTree(['/login']);
  };
}
