import { computed, inject, Injectable, Signal } from '@angular/core';
import { AuthService } from '../auth/auth-service';
import { decodeAccessToken } from '../auth/access-token';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly authService = inject(AuthService);

  // derived from the access token - updates automatically on login, logout and token refresh
  public readonly user: Signal<IUser | null> = computed(() => {
    const accessToken = this.authService.accessToken();
    return accessToken ? UserService.toUser(accessToken.token) : null;
  });
  public readonly isLoggedIn: Signal<boolean> = computed(() => this.user() !== null);

  /**
   * Reactive when called within a template, computed or effect (because it reads the user signal).
   */
  public hasRole(role: UserRole): boolean {
    // this works because we assume that higher roles contain all permissions of lower roles
    // and the enum values are ordered accordingly
    return (this.user()?.role ?? UserRole.Unknown) >= role;
  }

  private static toUser(accessToken: string): IUser | null {
    const claims = decodeAccessToken(accessToken);
    if (!claims) {
      return null;
    }

    const roles = claims.role;
    const role = roles.includes('Admin') ? UserRole.Admin
      : roles.includes('User') ? UserRole.User
        : roles.includes('Guest') ? UserRole.Guest
          : UserRole.Unknown;

    return {
      id: claims.sub,
      username: claims.name,
      role
    };
  }
}

export interface IUser {
  id: string;
  username: string;
  role: UserRole;
}

export enum UserRole {
  // has to be the lowest value, so it never satisfies a role check
  Unknown = -1,
  Guest = 0,
  User = 5,
  Admin = 20
}
