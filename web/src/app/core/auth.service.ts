import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { UserInfo } from './api.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly currentUser = signal<UserInfo | null>(null);
  readonly isAdmin = computed(() => this.currentUser()?.roles.includes('Admin') ?? false);

  // 🚀 โหลดผู้ใช้ปัจจุบันตอนเริ่มแอป (401 = ยังไม่ login)
  async init(): Promise<void> {
    try {
      this.currentUser.set(await firstValueFrom(this.http.get<UserInfo>('/api/auth/me')));
    } catch {
      this.currentUser.set(null);
    }
  }

  async login(userName: string, password: string): Promise<void> {
    const u = await firstValueFrom(this.http.post<UserInfo>('/api/auth/login', { userName, password }));
    this.currentUser.set(u);
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } finally {
      this.currentUser.set(null);
      await this.router.navigate(['/login']);
    }
  }
}
