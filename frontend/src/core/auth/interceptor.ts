import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { AuthService } from './auth-service';
import { API_BASE_URL } from '../const';

export const TOKEN_INTERCEPTION_DISABLED = new HttpContextToken<boolean>(() => false);

export const tokenInterceptor: HttpInterceptorFn = (req, next) => {
  // Only ever send our token to our own API - never to other servers (CDNs, third party APIs, ...)!
  if (req.context.get(TOKEN_INTERCEPTION_DISABLED) || !req.url.startsWith(API_BASE_URL)) {
    return next(req);
  }

  // inject has to be called synchronously, before any async operation
  const authService = inject(AuthService);

  return from(authService.getAccessToken()).pipe(
    switchMap(token => next(token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req))
  );
};
