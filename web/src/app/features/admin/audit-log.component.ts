import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuditDetail, AuditItem } from '../../core/api.models';
import { errorMessage } from '../../core/error.interceptor';
import { AdminService } from './admin.service';

@Component({
  selector: 'app-audit-log',
  imports: [DatePipe],
  template: `
    <h2>Audit log</h2>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <table>
      <tr><th>เวลา</th><th>ผู้ใช้</th><th>Action</th><th>สถานะ</th><th>Provider / Model</th><th>จำนวน mask</th><th>NER</th><th>ข้อความ masked</th><th></th></tr>
      @for (a of items(); track a.id) {
        <tr>
          <td>{{ a.createdAt | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
          <td>{{ a.userName }}</td>
          <td>{{ actionText(a.action) }}</td>
          <td>{{ a.status }}</td>
          <td>{{ a.provider ? a.provider + ' / ' + a.model : '-' }}</td>
          <td>{{ countText(a) }}</td>
          <td>{{ a.action === 'chat_send' ? (a.nerEnabled ? 'เปิด' : 'ปิด') : '-' }}</td>
          <td><pre>{{ a.maskedPayload }}</pre></td>
          <td><button type="button" (click)="open(a.id)">ดู payload</button></td>
        </tr>
      }
    </table>
    <p>
      <button type="button" (click)="go(page() - 1)" [disabled]="page() <= 1">ก่อนหน้า</button>
      หน้า {{ page() }} (ทั้งหมด {{ total() }} รายการ)
      <button type="button" (click)="go(page() + 1)" [disabled]="page() * size >= total()">ถัดไป</button>
    </p>

    @if (detail(); as d) {
      <div class="card">
        <strong>Payload ที่ส่งออก LLM จริง (id {{ d.id }})</strong>
        <p>SHA-256: <code>{{ d.payloadSha256 }}</code></p>
        @for (m of d.outboundPayload ?? []; track $index) {
          <p><b>{{ m.role }}</b></p>
          <!-- ⚠️ render เป็น text เท่านั้น -->
          <pre>{{ m.content }}</pre>
        } @empty {
          <p>ไม่มี payload (ไม่ได้ส่งออก)</p>
        }
        <button type="button" (click)="detail.set(null)">ปิด</button>
      </div>
    }
  `,
})
export class AuditLogComponent {
  private admin = inject(AdminService);
  readonly size = 50;
  items = signal<AuditItem[]>([]);
  total = signal(0);
  page = signal(1);
  detail = signal<AuditDetail | null>(null);
  error = signal('');

  constructor() {
    void this.go(1);
  }

  // 🔄 แปลงชนิดเหตุการณ์เป็นข้อความอ่านง่าย (ไม่รู้จัก = แสดงค่าเดิม)
  actionText(action: string): string {
    const labels: Record<string, string> = {
      chat_send: 'ส่งแชท',
      report_create: 'สร้าง PDF',
      report_download: 'ดาวน์โหลด PDF',
    };
    return labels[action] ?? action;
  }

  countText(a: AuditItem): string {
    return Object.entries(a.maskCounts ?? {}).map(([k, v]) => `${k}:${v}`).join(', ');
  }

  async go(p: number): Promise<void> {
    try {
      const r = await firstValueFrom(this.admin.auditLogs(p, this.size));
      this.items.set(r.items);
      this.total.set(r.total);
      this.page.set(p);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  async open(id: number): Promise<void> {
    try {
      this.detail.set(await firstValueFrom(this.admin.auditDetail(id)));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
