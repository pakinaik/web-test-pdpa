import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Conversation, MessageDto, PreviewResponse } from '../../core/api.models';
import { errorMessage } from '../../core/error.interceptor';
import { ChatService } from './chat.service';
import { ConversationListComponent } from './conversation-list.component';
import { MaskPreviewComponent } from './mask-preview.component';
import { MessageListComponent } from './message-list.component';

@Component({
  selector: 'app-chat-page',
  imports: [FormsModule, ConversationListComponent, MessageListComponent, MaskPreviewComponent],
  template: `
    @if (!nerEnabled()) {
      <div class="banner-warn">⚠️ การตรวจชื่อทำงานไม่เต็มที่ (NER ปิดอยู่) ชื่อที่ไม่มีคำนำหน้าและไม่อยู่ในรายชื่อพนักงานอาจไม่ถูก mask</div>
    }
    <div class="chat-layout">
      <app-conversation-list [conversations]="conversations()" [activeId]="conversationId()" />
      <section>
        <app-message-list [messages]="messages()" />

        <div class="card">
          <textarea #box rows="5" class="full-width" placeholder="พิมพ์เรื่องร้องเรียน/ปัญหา"
            [ngModel]="text()" (ngModelChange)="onTextChange($event)"></textarea>
          @if (customValues().length) {
            <p>Mask เพิ่มเอง: @for (c of customValues(); track c) { <mark>{{ c }}</mark> }</p>
          }
          @if (error()) { <p class="error">{{ error() }}</p> }
          <button type="button" (click)="doPreview()" [disabled]="busy() || !text().trim()">ตรวจสอบก่อนส่ง</button>
          <button type="button" (click)="addSelection()" [disabled]="busy()">Mask ส่วนที่เลือกเพิ่ม</button>
          <button type="button" class="primary" (click)="doSend()" [disabled]="busy() || !canSend()">ส่ง</button>
        </div>

        @if (preview(); as p) {
          <app-mask-preview [text]="previewedText()" [preview]="p" />
        }
      </section>
    </div>
  `,
})
export class ChatPageComponent {
  private chat = inject(ChatService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private box = viewChild<ElementRef<HTMLTextAreaElement>>('box');

  conversations = signal<Conversation[]>([]);
  messages = signal<MessageDto[]>([]);
  conversationId = signal<string | null>(null);
  text = signal('');
  customValues = signal<string[]>([]);
  preview = signal<PreviewResponse | null>(null);
  previewedText = signal('');
  error = signal('');
  busy = signal(false);
  nerEnabled = signal(true);

  constructor() {
    void this.loadConversations();
    firstValueFrom(this.chat.health()).then((h) => this.nerEnabled.set(h.nerEnabled)).catch(() => undefined);
    // 📦 โหลดข้อความเมื่อ route :id เปลี่ยน
    this.route.paramMap.subscribe((pm) => {
      const id = pm.get('id');
      this.conversationId.set(id);
      this.resetDraft();
      void this.loadMessages(id);
    });
  }

  private resetDraft(): void {
    this.text.set('');
    this.customValues.set([]);
    this.preview.set(null);
    this.error.set('');
  }

  private async loadConversations(): Promise<void> {
    try {
      this.conversations.set(await firstValueFrom(this.chat.conversations()));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private async loadMessages(id: string | null): Promise<void> {
    if (!id) {
      this.messages.set([]);
      return;
    }
    try {
      this.messages.set(await firstValueFrom(this.chat.messages(id)));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  onTextChange(v: string): void {
    this.text.set(v);
    // 🔍 แก้ข้อความหลัง preview → ต้องตรวจใหม่ก่อนส่ง
    this.preview.set(null);
  }

  canSend(): boolean {
    return !!this.preview()?.guard.passed;
  }

  async doPreview(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const t = this.text();
      this.preview.set(
        await firstValueFrom(this.chat.preview({ conversationId: this.conversationId(), text: t, customValues: this.customValues() })),
      );
      this.previewedText.set(t);
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  // 🔄 เพิ่มข้อความที่เลือกใน textarea เป็น custom mask แล้ว preview ใหม่
  async addSelection(): Promise<void> {
    const el = this.box()?.nativeElement;
    if (!el) return;
    const sel = el.value.substring(el.selectionStart, el.selectionEnd).trim();
    if (!sel || sel.length > 200) {
      this.error.set('เลือกข้อความที่ต้องการ mask (ไม่เกิน 200 ตัวอักษร)');
      return;
    }
    if (!this.customValues().includes(sel) && this.customValues().length < 20) {
      this.customValues.update((v) => [...v, sel]);
    }
    await this.doPreview();
  }

  async doSend(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const r = await firstValueFrom(
        this.chat.send({ conversationId: this.conversationId(), text: this.text(), customValues: this.customValues() }),
      );
      this.messages.update((m) => [...m, r.userMessage, r.assistantMessage]);
      this.resetDraft();
      await this.loadConversations();
      if (this.conversationId() !== r.conversationId) {
        await this.router.navigate(['/chat', r.conversationId]);
      }
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
