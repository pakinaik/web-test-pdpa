import { Component, inject, input, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MessageDto } from '../../core/api.models';
import { errorMessage } from '../../core/error.interceptor';
import { ChatService } from './chat.service';

@Component({
  selector: 'app-message-list',
  template: `
    @for (m of messages(); track m.id) {
      <div class="card" [class.msg-user]="m.role === 'user'" [class.msg-ai]="m.role !== 'user'">
        <small>{{ m.role === 'user' ? 'คุณ' : 'ผู้ช่วย AI' }}</small>
        <!-- ⚠️ render เป็น text เท่านั้น ห้าม innerHTML (กัน XSS) -->
        <pre>{{ m.content }}</pre>
        @if (m.reportId; as rid) {
          <button type="button" (click)="download(rid)" [disabled]="downloading() === rid">📄 ดาวน์โหลด PDF</button>
          <small> (ไฟล์มีอายุ 24 ชั่วโมง)</small>
        }
        @if (downloadError() && downloadErrorFor() === m.reportId) { <p class="error">{{ downloadError() }}</p> }
      </div>
    }
  `,
})
export class MessageListComponent {
  private chat = inject(ChatService);
  messages = input.required<MessageDto[]>();
  downloading = signal<string | null>(null);
  downloadError = signal('');
  downloadErrorFor = signal<string | null>(null);

  // 📤 R8: ดึงไฟล์เป็น blob แล้วให้ browser บันทึก — 🧹 revoke object URL ทันทีหลังใช้
  async download(id: string) {
    this.downloading.set(id);
    this.downloadError.set('');
    try {
      const blob = await firstValueFrom(this.chat.downloadReport(id));
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report-${id}.pdf`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch (err) {
      // 🔍 error body เป็น blob อ่าน code ไม่ได้ — 404 = ไม่พบ/หมดอายุ
      this.downloadErrorFor.set(id);
      this.downloadError.set(
        err instanceof HttpErrorResponse && err.status === 404 ? 'ไม่พบไฟล์รายงาน หรือไฟล์หมดอายุแล้ว' : errorMessage(err),
      );
    } finally {
      this.downloading.set(null);
    }
  }
}
