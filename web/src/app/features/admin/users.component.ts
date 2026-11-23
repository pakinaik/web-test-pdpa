import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AdminUser, CreateUserRequest } from '../../core/api.models';
import { errorMessage } from '../../core/error.interceptor';
import { AdminService } from './admin.service';

@Component({
  selector: 'app-users',
  imports: [FormsModule],
  template: `
    <h2>ผู้ใช้</h2>
    <form class="card" (ngSubmit)="create()">
      <input name="u" [(ngModel)]="form.userName" placeholder="ชื่อผู้ใช้" />
      <input name="p" type="password" [(ngModel)]="form.password" placeholder="รหัสผ่าน" autocomplete="new-password" />
      <input name="fth" [(ngModel)]="form.firstNameTh" placeholder="ชื่อ (ไทย)" />
      <input name="lth" [(ngModel)]="form.lastNameTh" placeholder="นามสกุล (ไทย)" />
      <input name="fen" [(ngModel)]="form.firstNameEn" placeholder="First name" />
      <input name="len" [(ngModel)]="form.lastNameEn" placeholder="Last name" />
      <select name="r" [(ngModel)]="form.role"><option>Employee</option><option>Admin</option></select>
      <button class="primary" type="submit">สร้างผู้ใช้</button>
      @if (error()) { <p class="error">{{ error() }}</p> }
    </form>
    <table>
      <tr><th>ชื่อผู้ใช้</th><th>ชื่อที่แสดง</th><th>Role</th></tr>
      @for (u of users(); track u.id) {
        <tr><td>{{ u.userName }}</td><td>{{ u.displayName }}</td><td>{{ u.roles.join(', ') }}</td></tr>
      }
    </table>
  `,
})
export class UsersComponent {
  private admin = inject(AdminService);
  users = signal<AdminUser[]>([]);
  error = signal('');
  form: CreateUserRequest = this.blank();

  constructor() {
    void this.load();
  }

  private blank(): CreateUserRequest {
    return { userName: '', password: '', firstNameTh: '', lastNameTh: '', firstNameEn: '', lastNameEn: '', role: 'Employee' };
  }

  async load(): Promise<void> {
    this.users.set(await firstValueFrom(this.admin.users()));
  }

  async create(): Promise<void> {
    this.error.set('');
    try {
      await firstValueFrom(this.admin.createUser(this.form));
      this.form = this.blank();
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
