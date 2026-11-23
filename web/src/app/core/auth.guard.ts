import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// 🔍 ต้อง login ก่อน
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.currentUser() ? true : inject(Router).createUrlTree(['/login']);
};
