import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    @if (auth.currentUser(); as u) {
      <header class="topbar">
        <strong>WebTestPDPA</strong>
        <nav>
          <a routerLink="/chat" routerLinkActive="active">แชท</a>
          @if (auth.isAdmin()) {
            <a routerLink="/admin/users" routerLinkActive="active">ผู้ใช้</a>
            <a routerLink="/admin/audit" routerLinkActive="active">Audit log</a>
            <a routerLink="/admin/pii-tests" routerLinkActive="active">PII test bench</a>
          }
        </nav>
        <span class="spacer"></span>
        <span>{{ u.displayName }}</span>
        <button type="button" (click)="auth.logout()">ออกจากระบบ</button>
      </header>
    }
    <main><router-outlet /></main>
  `,
})
export class App {
  protected auth = inject(AuthService);
}
