# 🚀 E2E: docker compose + LLM_PROVIDER=mock — ตรวจว่าไม่มี PII รั่วไป outbound/audit/logs
# ⚠️ ไม่ down -v อัตโนมัติ (ให้ผู้ใช้เลือกเอง)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# 🔍 อ่านค่าจาก .env
$envMap = @{}
Get-Content (Join-Path $root '.env') -Encoding UTF8 | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { $envMap[$Matches[1]] = $Matches[2] }
}
if ($envMap['LLM_PROVIDER'] -ne 'mock') {
    Write-Host "❌ e2e ต้องรันด้วย LLM_PROVIDER=mock เท่านั้น (ตอนนี้: $($envMap['LLM_PROVIDER']))"
    exit 2
}
$port = if ($envMap['WEB_PORT']) { $envMap['WEB_PORT'] } else { '8080' }
$base = "http://localhost:$port"

$script:failed = 0
function Step([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host "PASS  $name" } else { Write-Host "FAIL  $name  $detail"; $script:failed++ }
}

# 📤 เรียก API คืน Status + Body (parse JSON) — รองรับ error status ใน PowerShell 5.1
function Call-Api([string]$method, [string]$path, $body, $session) {
    $params = @{ Uri = "$base$path"; Method = $method; UseBasicParsing = $true; ErrorAction = 'Stop' }
    if ($session) { $params['WebSession'] = $session }
    if ($null -ne $body) {
        $json = $body | ConvertTo-Json -Depth 6 -Compress
        $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
        $params['ContentType'] = 'application/json; charset=utf-8'
    }
    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $text = $r.Content
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $status = [int]$resp.StatusCode
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $text = $reader.ReadToEnd()
        $reader.Dispose()
    }
    $obj = $null
    if ($text) { try { $obj = $text | ConvertFrom-Json } catch { $obj = $null } }
    return @{ Status = $status; Body = $obj; Raw = $text }
}

$phone = '081-234-5678'
$thaiId = '1101700234568'
$email = 'somchai@example.com'
$pii = @($phone, $thaiId, $email, 'สมชาย')

# 1) 🚀 ขึ้น stack แล้วรอ health
docker compose up --build -d
if ($LASTEXITCODE -ne 0) { Step 'compose up --build' $false; exit 1 }
$ready = $false
$deadline = (Get-Date).AddMinutes(5)
while ((Get-Date) -lt $deadline) {
    try {
        $h = Call-Api 'GET' '/api/health' $null $null
        if ($h.Status -eq 200 -and $h.Body.status -eq 'ok' -and $h.Body.pii -eq 'up') { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 3
}
Step '1 stack พร้อม (/api/health = ok, pii = up)' $ready
if (-not $ready) { exit 1 }

# 2) ⚠️ login admin
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$login = Call-Api 'POST' '/api/auth/login' @{ userName = $envMap['ADMIN_USERNAME']; password = $envMap['ADMIN_PASSWORD'] } $session
Step '2 login admin' ($login.Status -eq 200)
if ($login.Status -ne 200) { exit 1 }

# 3) 📤 ส่งข้อความที่มี PII ครบ
$text1 = "คุณสมชาย ใจดี เบอร์ $phone บัตร $thaiId อีเมล $email โดนหักเงินเดือนผิด"
$s1 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $null; text = $text1; customValues = @() } $session
$masked1 = "$($s1.Body.userMessage.maskedContent)|$($s1.Body.assistantMessage.maskedContent)"
$noLeak = $true
foreach ($v in $pii) { if ($masked1.Contains($v)) { $noLeak = $false } }
Step '3 send: masked ไม่มี PII, มี [PHONE_1], unmask ได้' (
    $s1.Status -eq 200 -and $noLeak -and $masked1.Contains('[PHONE_1]') -and $s1.Body.assistantMessage.content.Contains($phone)
) "status=$($s1.Status)"
$convId = $s1.Body.conversationId

# 4) 🔄 ข้อความที่ 2 ในบทสนทนาเดิม — token เดิม
$s2 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $convId; text = 'ขอสอบถามเพิ่ม เบอร์เดิม 0812345678'; customValues = @() } $session
Step '4 token consistent ([PHONE_1])' ($s2.Status -eq 200 -and $s2.Body.userMessage.maskedContent.Contains('[PHONE_1]')) "status=$($s2.Status)"

# 5) 🔍 audit log
$a = Call-Api 'GET' '/api/admin/audit-logs?page=1&pageSize=5' $null $session
$latest = $a.Body.items[0]
$auditOk = ($a.Status -eq 200 -and $latest.status -eq 'sent' -and $latest.nerEnabled -eq $true)
foreach ($v in $pii) { if ("$($latest.maskedPayload)".Contains($v)) { $auditOk = $false } }
Step '5a audit: sent + nerEnabled + maskedPayload ไม่มี PII' $auditOk
$d = Call-Api 'GET' "/api/admin/audit-logs/$($latest.id)" $null $session
$payloadText = ($d.Body.outboundPayload | ForEach-Object { "$($_.role):$($_.content)" }) -join '|'
$payloadOk = ($d.Status -eq 200 -and $d.Body.outboundPayload.Count -ge 4 -and $d.Body.outboundPayload[0].role -eq 'system' -and $d.Body.payloadSha256.Length -eq 64)
foreach ($v in $pii) { if ($payloadText.Contains($v)) { $payloadOk = $false } }
Step '5b audit detail: outboundPayload = system + history + current, ไม่มี PII' $payloadOk

# 6) ⚠️ guard: เลข 13 หลัก checksum ผิด
$s3 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $convId; text = 'เลขคำร้อง 1101700234567'; customValues = @() } $session
Step '6 guard block (422 GUARD_BLOCKED)' ($s3.Status -eq 422 -and "$($s3.Raw)".Contains('GUARD_BLOCKED')) "status=$($s3.Status)"

# 7) ⚠️ fail-closed: pii ล่ม
docker compose stop pii | Out-Null
$s4 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $convId; text = "ทดสอบ เบอร์ $phone"; customValues = @() } $session
Step '7 pii ล่ม -> 503 PII_UNAVAILABLE' ($s4.Status -eq 503 -and "$($s4.Raw)".Contains('PII_UNAVAILABLE')) "status=$($s4.Status)"
docker compose start pii | Out-Null

# 🚀 รอ pii กลับมา up ก่อนขั้นถัดไป (หลัง start pii ในขั้น 7)
# ⚠️ /api/health cache 30 วิ อาจยังตอบ up ทั้งที่ pii เพิ่งเริ่ม -> ตรวจ healthcheck ของ container ก่อน แล้วค่อยยืนยันผ่าน /api/health
$piiUp = $false
$deadline = (Get-Date).AddMinutes(2)
while ((Get-Date) -lt $deadline) {
    try {
        $cid = (docker compose ps -q pii | Out-String).Trim()
        $hs = if ($cid) { (docker inspect -f '{{.State.Health.Status}}' $cid | Out-String).Trim() } else { '' }
        if ($hs -eq 'healthy') {
            $h = Call-Api 'GET' '/api/health' $null $null
            if ($h.Status -eq 200 -and $h.Body.status -eq 'ok' -and $h.Body.pii -eq 'up') { $piiUp = $true; break }
        }
    } catch { }
    Start-Sleep -Seconds 3
}
Step '7b pii กลับมา up' $piiUp
if (-not $piiUp) { exit 1 }

# 📦 อ่านค่าจริงของพนักงานสมมติจาก seed (ไม่ hardcode) — ใช้เทียบว่าไม่รั่ว; พิมพ์เฉพาะผล PASS/FAIL ห้ามพิมพ์ค่า
$empSeed = Get-Content (Join-Path $root 'api/src/WebTestPdpa.Api/SeedData/employees.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Get-EmpLeakValues([string]$code) {
    $e = $empSeed | Where-Object { $_.employeeCode -eq $code } | Select-Object -First 1
    $vals = @($e.firstNameTh, $e.lastNameTh, $e.firstNameEn, $e.lastNameEn, $e.nationalId, $e.phone, $e.email, $e.bankAccountNo)
    # ⚠️ ค่าสั้น/คำทั่วไป (เช่น chronic_disease = 'ไม่มี') ไม่ใส่ในรายการ leak เพราะชนกับข้อความปกติ — xUnit ตรวจครบทุก field แล้ว
    return @($vals | Where-Object { $_ -and "$_".Length -ge 4 })
}
function Test-NoLeak([string]$text, $values) {
    foreach ($v in $values) { if ($text.Contains($v)) { return $false } }
    return $true
}
# 📦 ดึง audit detail ล่าสุด (ใช้ session admin)
function Get-LatestAuditDetail() {
    $list = Call-Api 'GET' '/api/admin/audit-logs?page=1&pageSize=1' $null $session
    if ($list.Status -ne 200 -or -not $list.Body.items) { return $null }
    $det = Call-Api 'GET' "/api/admin/audit-logs/$($list.Body.items[0].id)" $null $session
    if ($det.Status -ne 200) { return $null }
    return $det.Body
}
$emp001Vals = Get-EmpLeakValues 'emp001'
$emp002Vals = Get-EmpLeakValues 'emp002'
$emp005Vals = Get-EmpLeakValues 'emp005'
$emp001 = $empSeed | Where-Object { $_.employeeCode -eq 'emp001' } | Select-Object -First 1

# 8) 🔍 .env ต้องมี EMPLOYEE_SEED_PASSWORD (ไม่พิมพ์ค่า)
$empPwd = $envMap['EMPLOYEE_SEED_PASSWORD']
$hasEmpPwd = -not [string]::IsNullOrEmpty($empPwd)
Step '8 .env มี EMPLOYEE_SEED_PASSWORD' $hasEmpPwd 'ไม่พบ — รัน scripts/new-env.ps1 เพื่อเติม key (ไม่ทับบรรทัดเดิม)'
if (-not $hasEmpPwd) { exit 1 }

# 9) 🔄 emp001 ถามข้อมูลอ่อนไหวของตัวเอง — masked ต้องมี token, content ต้อง unmask เป็นค่าจริง
$sessEmp = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginEmp = Call-Api 'POST' '/api/auth/login' @{ userName = 'emp001'; password = $empPwd } $sessEmp
Step '9a login emp001' ($loginEmp.Status -eq 200)
if ($loginEmp.Status -ne 200) { exit 1 }
$e1 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $null; text = 'ขอดูศาสนา กรุ๊ปเลือด และโรคประจำตัวของฉัน'; customValues = @() } $sessEmp
$m9 = "$($e1.Body.userMessage.maskedContent)|$($e1.Body.assistantMessage.maskedContent)"
$ok9 = ($e1.Status -eq 200 -and $m9.Contains('[RELIGION_') -and $m9.Contains('[HEALTH_') -and
    (Test-NoLeak $m9 $emp001Vals) -and -not $m9.Contains("$($emp001.religion)") -and
    "$($e1.Body.assistantMessage.content)".Contains("$($emp001.religion)"))
Step '9b emp001 ถามข้อมูลตัวเอง: masked มี [RELIGION_]/[HEALTH_] ไม่มีค่าจริง, content unmask ได้' $ok9 "status=$($e1.Status)"
$ad9 = Get-LatestAuditDetail
$ids9 = @($ad9.contextEmployeeIds)
Step '9c audit: contextEmployeeIds = 1 คน (emp001)' ($null -ne $ad9 -and $ids9.Count -eq 1)

# 10) ⚠️ emp001 ถามเงินเดือนของคนอื่น — ต้องได้ context ของตัวเองเท่านั้น
$e2 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $null; text = 'ขอดูเงินเดือนของ emp002'; customValues = @() } $sessEmp
$ad10 = Get-LatestAuditDetail
$ids10 = @($ad10.contextEmployeeIds)
$pay10 = ($ad10.outboundPayload | ForEach-Object { "$($_.role):$($_.content)" }) -join '|'
$ok10 = ($e2.Status -eq 200 -and $null -ne $ad10 -and $ids10.Count -eq 1 -and $ids9.Count -eq 1 -and $ids10[0] -eq $ids9[0] -and
    (Test-NoLeak $pay10 $emp001Vals) -and (Test-NoLeak $pay10 $emp002Vals))
Step '10 emp001 ถามของ emp002: context = emp001 เท่านั้น, outbound ไม่มีค่าจริงของ emp001/emp002' $ok10 "status=$($e2.Status)"

# 11) 📤 HR (emp020) ถามข้อมูลของ emp005
$sessHr = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginHr = Call-Api 'POST' '/api/auth/login' @{ userName = 'emp020'; password = $empPwd } $sessHr
Step '11a login emp020 (HR)' ($loginHr.Status -eq 200)
if ($loginHr.Status -ne 200) { exit 1 }
$h1 = Call-Api 'POST' '/api/chat/send' @{ conversationId = $null; text = 'emp005 ลาพักร้อนไปกี่วัน'; customValues = @() } $sessHr
$ad11 = Get-LatestAuditDetail
$ids11 = @($ad11.contextEmployeeIds)
$pay11 = ($ad11.outboundPayload | ForEach-Object { "$($_.role):$($_.content)" }) -join '|'
$ok11 = ($h1.Status -eq 200 -and $null -ne $ad11 -and $ids11.Count -eq 1 -and $ids9.Count -eq 1 -and $ids11[0] -ne $ids9[0] -and
    $ad11.outboundPayload.Count -ge 2 -and $ad11.outboundPayload[1].role -eq 'system' -and
    (Test-NoLeak $pay11 $emp005Vals))
Step '11b HR ถาม emp005: context = 1 คน (ไม่ใช่ emp001), system #2, ไม่มีค่าจริงของ emp005' $ok11 "status=$($h1.Status)"

# 12) 🔍 logs ต้องไม่มี PII (รวมค่าของ emp001/emp002/emp005 — ชื่อ, เลขบัตร, เบอร์, บัญชี, อีเมล)
$logs = (docker compose logs api pii web 2>&1) | Out-String
$logOk = $true
foreach ($v in ($pii + $emp001Vals + $emp002Vals + $emp005Vals)) {
    if ($logs.Contains($v)) { $logOk = $false; Write-Host "  พบสตริงต้องห้ามใน logs: ความยาว $($v.Length) ตัวอักษร" }
}
Step '12 docker compose logs ไม่มี PII' $logOk

# 🔍 สรุป
if ($script:failed -gt 0) { Write-Host "❌ FAIL $($script:failed) ขั้น"; exit 1 }
Write-Host "✅ E2E PASS ทุกขั้น (stack ยังรันอยู่ — ปิดเองด้วย 'docker compose down' หรือ 'down -v' เพื่อล้าง volume)"
exit 0
