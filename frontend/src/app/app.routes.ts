import { Routes } from '@angular/router';
import { Login } from "./login/login";
import { Home } from "./home/home";
import { Products } from "./products/products";
import { roleGuard } from "../core/auth/role.guard";
import { UserRole } from "../core/user/user-service";

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'home', component: Home },
  { path: 'products', component: Products, canActivate: [roleGuard(UserRole.User)] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
];
