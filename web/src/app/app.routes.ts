import { Routes } from '@angular/router';
import { adminGuard } from './core/admin.guard';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  { path: 'login', loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent) },
  {
    path: 'chat',
    canActivate: [authGuard],
    loadComponent: () => import('./features/chat/chat-page.component').then((m) => m.ChatPageComponent),
  },
  {
    path: 'chat/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/chat/chat-page.component').then((m) => m.ChatPageComponent),
  },
  {
    path: 'admin/users',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/users.component').then((m) => m.UsersComponent),
  },
  {
    path: 'admin/audit',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/audit-log.component').then((m) => m.AuditLogComponent),
  },
  {
    path: 'admin/pii-tests',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/pii-bench.component').then((m) => m.PiiBenchComponent),
  },
  { path: '**', redirectTo: 'chat' },
];
