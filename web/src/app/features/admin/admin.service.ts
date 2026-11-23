import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AdminUser, AuditDetail, AuditPage, BenchReport, CreateUserRequest } from '../../core/api.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);

  // 📦 ดึงข้อมูล
  users() {
    return this.http.get<AdminUser[]>('/api/admin/users');
  }
  auditLogs(page: number, pageSize: number) {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<AuditPage>('/api/admin/audit-logs', { params });
  }
  auditDetail(id: number) {
    return this.http.get<AuditDetail>(`/api/admin/audit-logs/${id}`);
  }

  // 📤 ส่งไป API
  createUser(req: CreateUserRequest) {
    return this.http.post('/api/admin/users', req);
  }
  runBench() {
    return this.http.post<BenchReport>('/api/admin/pii-tests/run', {});
  }
}
