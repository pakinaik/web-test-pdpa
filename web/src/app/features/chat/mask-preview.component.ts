import { Component, computed, input } from '@angular/core';
import { PreviewEntity, PreviewResponse } from '../../core/api.models';

interface Segment {
  text: string;
  entity: PreviewEntity | null;
}

@Component({
  selector: 'app-mask-preview',
  template: `
    <div class="card">
      <strong>ส่วนที่จะถูก mask</strong>
      <!-- ⚠️ สร้างเป็น segment แล้ว render ด้วย @for (ไม่ใช้ innerHTML) -->
      <pre>@for (s of segments(); track $index) {@if (s.entity) {<mark [title]="s.entity.type + ' → ' + s.entity.token">{{ s.text }}</mark>} @else {<span>{{ s.text }}</span>}}</pre>
      <p><strong>ข้อความที่จะส่งให้ AI</strong></p>
      <pre>{{ preview().maskedText }}</pre>
      <ul>
        @for (e of preview().entities; track $index) {
          <li>{{ e.type }} → {{ e.token }} ({{ e.source }})</li>
        }
      </ul>
      @if (!preview().guard.passed) {
        <p class="error">ตรวจพบรูปแบบข้อมูลส่วนบุคคลที่ยังไม่ถูก mask ({{ preview().guard.reasons.join(', ') }}) — เลือกข้อความนั้นแล้วกด "Mask ส่วนที่เลือกเพิ่ม"</p>
      }
    </div>
  `,
})
export class MaskPreviewComponent {
  text = input.required<string>();
  preview = input.required<PreviewResponse>();

  // 🔄 แบ่งข้อความตาม offset (UTF-16 ตรงกับ index ของ JS string)
  segments = computed<Segment[]>(() => {
    const t = this.text();
    const out: Segment[] = [];
    let pos = 0;
    for (const e of [...this.preview().entities].sort((a, b) => a.start - b.start)) {
      if (e.start < pos || e.end > t.length) continue;
      if (e.start > pos) out.push({ text: t.slice(pos, e.start), entity: null });
      out.push({ text: t.slice(e.start, e.end), entity: e });
      pos = e.end;
    }
    if (pos < t.length) out.push({ text: t.slice(pos), entity: null });
    return out;
  });
}
