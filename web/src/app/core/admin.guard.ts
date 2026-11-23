import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// 🔍 เฉพาะ Admin
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (!auth.currentUser()) return inject(Router).createUrlTree(['/login']);
  return auth.isAdmin() ? true : inject(Router).createUrlTree(['/chat']);
};
