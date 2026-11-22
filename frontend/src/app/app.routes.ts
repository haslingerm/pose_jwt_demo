import { Routes } from '@angular/router';
import { Login } from './login/login';
import { Home } from './home/home';
import { roleGuard } from '../core/auth/role.guard';
import { UserRole } from '../core/user/user-service';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'home', component: Home },
  {
    path: 'products',
    // lazy loaded: the code is only downloaded when the route is visited (and the guard allows it)
    loadComponent: () => import('./products/products').then(m => m.Products),
    canActivate: [roleGuard(UserRole.User)]
  },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' },
];
