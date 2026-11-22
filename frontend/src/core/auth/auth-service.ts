import { computed, inject, Injectable, Signal, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Duration, Instant } from '@js-joda/core';
import { z } from 'zod';
import { TOKEN_INTERCEPTION_DISABLED } from './interceptor';
import { API_BASE_URL } from '../const';
import { LocalStorage } from '../shared/local-storage';
import { decodeAccessToken } from './access-token';

export type LoginResult = 'success' | 'invalidCredentials' | 'tooManyAttempts' | 'error';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private static readonly tokensKey = 'tokens';
  // refresh a bit before the access token expires, so it cannot expire while a request is on its way
  private static readonly expirationThreshold = Duration.ofMinutes(2);
  private readonly baseUrl = `${API_BASE_URL}/auth`;
  private readonly client = inject(HttpClient);
  private readonly storage = new LocalStorage('Auth');

  // The single source of truth for the login state. Everything else (current user, roles, ...) is derived from it,
  // so the whole UI updates automatically when we log in, log out or a token refresh fails.
  private readonly tokens = signal<ITokens | null>(this.loadTokens());
  public readonly accessToken: Signal<IToken | null> = computed(() => this.tokens()?.accessToken ?? null);

  // a refresh request which is currently running, see getAccessToken
  private runningRefresh: Promise<ITokens | null> | null = null;

  /**
   * Returns a valid access token - refreshing it first, if necessary.
   * Returns null if the user is not logged in (anymore).
   */
  public async getAccessToken(): Promise<string | null> {
    const tokens = this.tokens();
    if (tokens === null) {
      return null;
    }

    if (!expiresWithin(tokens.accessToken, AuthService.expirationThreshold)) {
      return tokens.accessToken.token;
    }

    // Several requests might need a new token at the same time (e.g. when a page loads).
    // Each refresh token can only be used once (rotation), so there must only be a single refresh request:
    // all callers wait for the same one.
    this.runningRefresh ??= this.refreshTokens(tokens)
      .finally(() => this.runningRefresh = null);

    const newTokens = await this.runningRefresh;
    return newTokens?.accessToken.token ?? null;
  }

  public async login(username: string, password: string): Promise<LoginResult> {
    this.setTokens(null);

    try {
      const response = await firstValueFrom(this.client.post(`${this.baseUrl}/logins`, { username, password },
        { context: AuthService.withoutToken() }));
      this.setTokens(TokensWire.parse(response));
      return 'success';
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) {
        throw error;
      }

      switch (error.status) {
        case HttpStatusCode.Unauthorized: {
          return 'invalidCredentials';
        }
        case HttpStatusCode.TooManyRequests: {
          return 'tooManyAttempts';
        }
        default: {
          console.error(`Failed to login: ${error.status}`);
          return 'error';
        }
      }
    }
  }

  public async logout(): Promise<void> {
    const tokens = this.tokens();
    // we always log out locally, even if the backend can't be reached
    this.setTokens(null);

    const username = tokens ? decodeAccessToken(tokens.accessToken.token)?.name : null;
    if (!tokens || !username) {
      return;
    }

    // Log out at the backend as well, which revokes the refresh token.
    // The access token stays valid until it expires (that's why its lifetime should be short).
    try {
      await firstValueFrom(this.client.post(`${this.baseUrl}/logouts`,
        { username, refreshToken: tokens.refreshToken.token },
        { context: AuthService.withoutToken() }));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) {
        throw error;
      }
      console.error(`Failed to logout at the backend: ${error.status}`);
    }
  }

  private async refreshTokens(tokens: ITokens): Promise<ITokens | null> {
    const username = decodeAccessToken(tokens.accessToken.token)?.name;
    if (isExpired(tokens.refreshToken) || !username) {
      console.log('Refresh token expired - user has to log in again');
      this.setTokens(null);
      return null;
    }

    try {
      const response = await firstValueFrom(this.client.post(`${this.baseUrl}/token-refreshes`,
        { username, refreshToken: tokens.refreshToken.token },
        { context: AuthService.withoutToken() }));
      const newTokens = TokensWire.parse(response);
      this.setTokens(newTokens);
      return newTokens;
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) {
        throw error;
      }

      if (error.status === HttpStatusCode.Unauthorized) {
        // the refresh token was revoked (e.g. logout in another tab) - we are logged out
        console.log('Refresh token invalid - user has to log in again');
        this.setTokens(null);
      } else {
        // e.g. backend not reachable - keep the tokens, maybe it works next time
        console.error(`Failed to refresh token: ${error.status}`);
      }
      return null;
    }
  }

  private setTokens(tokens: ITokens | null): void {
    this.tokens.set(tokens);
    if (tokens) {
      // Local storage is easy to use and survives page reloads, but it can be read by any JavaScript running on
      // our page - so an XSS vulnerability would allow an attacker to steal the tokens.
      // An alternative is to keep the refresh token in an HttpOnly cookie, which JavaScript cannot access at all
      // (requires more backend work and CSRF protection) - see the Readme.
      this.storage.set(AuthService.tokensKey, tokens);
    } else {
      this.storage.remove(AuthService.tokensKey);
    }
  }

  private loadTokens(): ITokens | null {
    try {
      const result = TokensWire.safeParse(this.storage.get(AuthService.tokensKey));
      // no need to keep tokens which cannot be refreshed anymore
      return result.success && !isExpired(result.data.refreshToken) ? result.data : null;
    } catch {
      // e.g. invalid date format
      return null;
    }
  }

  private static withoutToken(): HttpContext {
    // we don't send the access token to the auth endpoints, the credentials are in the request body
    return new HttpContext().set(TOKEN_INTERCEPTION_DISABLED, true);
  }
}

function isExpired(token: IToken): boolean {
  return Instant.now().isAfter(token.expiration);
}

function expiresWithin(token: IToken, duration: Duration): boolean {
  return Instant.now().plus(duration).isAfter(token.expiration);
}

const TokenWire = z.object({
  token: z.string().min(1),
  expiration: z.string().transform(s => Instant.parse(s))
});

const TokensWire = z.object({
  accessToken: TokenWire,
  refreshToken: TokenWire
});

export type IToken = z.infer<typeof TokenWire>;
type ITokens = z.infer<typeof TokensWire>;
