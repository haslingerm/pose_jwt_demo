import {
  ApplicationConfig,
  provideZonelessChangeDetection,
} from "@angular/core";
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { tokenInterceptor } from "../core/auth/interceptor";

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([tokenInterceptor])),
    provideAnimationsAsync()
  ]
};
