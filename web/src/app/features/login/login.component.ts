import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/error.interceptor';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <form class="card login-box" (ngSubmit)="submit()">
      <h2>เข้าสู่ระบบ</h2>
      <p><input name="u" [(ngModel)]="userName" placeholder="ชื่อผู้ใช้" autocomplete="username" class="full-width" /></p>
      <p><input name="p" type="password" [(ngModel)]="password" placeholder="รหัสผ่าน" autocomplete="current-password" class="full-width" /></p>
      @if (error()) { <p class="error">{{ error() }}</p> }
      <button class="primary" type="submit" [disabled]="busy()">เข้าสู่ระบบ</button>
    </form>
  `,
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  userName = '';
  password = '';
  busy = signal(false);
  error = signal('');

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.userName, this.password);
      this.password = '';
      await this.router.navigate(['/chat']);
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
