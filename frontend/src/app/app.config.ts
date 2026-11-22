import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { tokenInterceptor } from '../core/auth/interceptor';

// No zone.js: zoneless change detection is the default since Angular 21, and since Angular 22 components use
// OnPush by default. Change detection is triggered by signals (and events/async pipes), so all state is kept in signals.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // the interceptor adds the access token to our API requests
    provideHttpClient(withInterceptors([tokenInterceptor])),
  ]
};
