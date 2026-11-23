import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// 🔄 แปลง code จาก API เป็นข้อความภาษาไทย
const MESSAGES: Record<string, string> = {
  INVALID_CREDENTIALS: 'ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง',
  VALIDATION_ERROR: 'ข้อมูลไม่ถูกต้อง (ข้อความว่างหรือยาวเกินกำหนด)',
  CONVERSATION_NOT_FOUND: 'ไม่พบบทสนทนานี้',
  GUARD_BLOCKED: 'ตรวจพบข้อมูลที่อาจเป็นข้อมูลส่วนบุคคลหลุดรอด จึงไม่ส่งออก — เลือกข้อความนั้นแล้วกด "Mask ส่วนที่เลือกเพิ่ม"',
  RATE_LIMITED: 'ส่งข้อความถี่เกินไป กรุณารอสักครู่',
  LLM_ERROR: 'ระบบ AI ตอบกลับไม่สำเร็จ (ข้อความของคุณถูกบันทึกแล้ว)',
  PII_UNAVAILABLE: 'ระบบตรวจข้อมูลส่วนบุคคลไม่พร้อมใช้งาน จึงยังไม่ส่งข้อความ',
  REPORT_NOT_FOUND: 'ไม่พบไฟล์รายงาน หรือไฟล์หมดอายุแล้ว',
  DUPLICATE_USER: 'มีชื่อผู้ใช้นี้อยู่แล้ว',
  PASSWORD_POLICY: 'รหัสผ่านไม่ผ่านเงื่อนไข (อย่างน้อย 10 ตัว มีตัวพิมพ์ใหญ่/เล็ก/ตัวเลข/สัญลักษณ์)',
};

export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const code = (err.error as { code?: string } | null)?.code;
    if (code && MESSAGES[code]) return MESSAGES[code];
    if (err.status === 403) return 'ไม่มีสิทธิ์ใช้งานส่วนนี้';
    if (err.status === 0) return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้';
  }
  return 'เกิดข้อผิดพลาด กรุณาลองใหม่';
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((err) => {
      // 🔍 401 ที่ไม่ใช่หน้า login/me → กลับไปหน้า login
      if (err instanceof HttpErrorResponse && err.status === 401 && !req.url.includes('/api/auth/')) {
        auth.currentUser.set(null);
        void router.navigate(['/login']);
      }
      return throwError(() => err);
    }),
  );
};
