// 📦 type ตาม API contract (task_plan.md)
export interface UserInfo {
  id: string;
  userName: string;
  displayName: string;
  roles: string[];
}

export interface HealthInfo {
  status: string;
  pii: 'up' | 'down';
  nerEnabled: boolean;
}

export interface Conversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface MessageDto {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  maskedContent: string;
  maskCounts: Record<string, number>;
  createdAt: string;
  // 📦 R8: id รายงาน PDF (null = ไม่มี/หมดอายุ)
  reportId?: string | null;
}

export interface PreviewEntity {
  type: string;
  start: number;
  end: number;
  token: string;
  source: string;
}

export interface PreviewResponse {
  maskedText: string;
  entities: PreviewEntity[];
  maskCounts: Record<string, number>;
  guard: { passed: boolean; reasons: string[] };
}

export interface SendResponse {
  conversationId: string;
  userMessage: MessageDto;
  assistantMessage: MessageDto;
  provider: string;
  model: string;
}

export interface ChatRequest {
  conversationId: string | null;
  text: string;
  customValues: string[];
}

export interface AdminUser {
  id: string;
  userName: string;
  displayName: string;
  roles: string[];
  isActive: boolean;
}

export interface CreateUserRequest {
  userName: string;
  password: string;
  firstNameTh: string;
  lastNameTh: string;
  firstNameEn: string;
  lastNameEn: string;
  role: 'Employee' | 'Admin';
}

export interface AuditItem {
  id: number;
  createdAt: string;
  userName: string;
  conversationId: string | null;
  // 📦 chat_send / report_create / report_download
  action: string;
  status: string;
  provider: string;
  model: string;
  maskCounts: Record<string, number>;
  maskedPayload: string | null;
  historyMessageCount: number;
  nerEnabled: boolean;
  guardReasons: string[] | null;
  errorCode: string | null;
  latencyMs: number | null;
}

export interface AuditDetail extends AuditItem {
  outboundPayload: { role: string; content: string }[] | null;
  payloadSha256: string | null;
}

export interface AuditPage {
  items: AuditItem[];
  total: number;
}

export interface TypeScore {
  type: string;
  tp: number;
  fp: number;
  fn: number;
  precision: number;
  recall: number;
  f1: number;
}

export interface BenchReport {
  totalCases: number;
  perType: TypeScore[];
  overall: { tp: number; fp: number; fn: number; precision: number; recall: number; f1: number };
  failures: {
    caseKey: string;
    missed: { type: string; value: string }[];
    unexpected: { type: string; value: string }[];
  }[];
}
