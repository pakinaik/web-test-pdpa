import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BenchReport } from '../../core/api.models';
import { errorMessage } from '../../core/error.interceptor';
import { AdminService } from './admin.service';

@Component({
  selector: 'app-pii-bench',
  template: `
    <h2>PII test bench</h2>
    <button class="primary" type="button" (click)="run()" [disabled]="busy()">รัน test</button>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (report(); as r) {
      <p>ทั้งหมด {{ r.totalCases }} เคส</p>
      <table>
        <tr><th>ประเภท</th><th>TP</th><th>FP</th><th>FN</th><th>Precision</th><th>Recall</th><th>F1</th></tr>
        @for (t of r.perType; track t.type) {
          <tr><td>{{ t.type }}</td><td>{{ t.tp }}</td><td>{{ t.fp }}</td><td>{{ t.fn }}</td><td>{{ t.precision }}</td><td>{{ t.recall }}</td><td>{{ t.f1 }}</td></tr>
        }
        <tr><th>รวม</th><th>{{ r.overall.tp }}</th><th>{{ r.overall.fp }}</th><th>{{ r.overall.fn }}</th><th>{{ r.overall.precision }}</th><th>{{ r.overall.recall }}</th><th>{{ r.overall.f1 }}</th></tr>
      </table>
      <h3>เคสที่ไม่ผ่าน</h3>
      <table>
        <tr><th>เคส</th><th>ตรวจไม่พบ</th><th>ตรวจเกิน</th></tr>
        @for (f of r.failures; track f.caseKey) {
          <tr>
            <td>{{ f.caseKey }}</td>
            <td>@for (m of f.missed; track $index) { <div>{{ m.type }}: {{ m.value }}</div> }</td>
            <td>@for (m of f.unexpected; track $index) { <div>{{ m.type }}: {{ m.value }}</div> }</td>
          </tr>
        }
      </table>
    }
  `,
})
export class PiiBenchComponent {
  private admin = inject(AdminService);
  report = signal<BenchReport | null>(null);
  busy = signal(false);
  error = signal('');

  async run(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      this.report.set(await firstValueFrom(this.admin.runBench()));
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
