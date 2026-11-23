import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ChatRequest, Conversation, HealthInfo, MessageDto, PreviewResponse, SendResponse } from '../../core/api.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private http = inject(HttpClient);

  // 📦 ดึงข้อมูล
  conversations() {
    return this.http.get<Conversation[]>('/api/conversations');
  }
  messages(id: string) {
    return this.http.get<MessageDto[]>(`/api/conversations/${id}/messages`);
  }
  health() {
    return this.http.get<HealthInfo>('/api/health');
  }

  // 📤 ส่งไป API
  preview(req: ChatRequest) {
    return this.http.post<PreviewResponse>('/api/chat/preview', req);
  }
  send(req: ChatRequest) {
    return this.http.post<SendResponse>('/api/chat/send', req);
  }

  // 📦 R8: ดาวน์โหลดรายงาน PDF (cookie auth ไปกับ request เดียวกัน)
  downloadReport(id: string) {
    return this.http.get(`/api/reports/${id}`, { responseType: 'blob' });
  }
}
