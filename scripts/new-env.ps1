# 🚀 สร้าง .env จาก .env.example พร้อมสุ่ม secret (ไม่ทับ .env ที่มีอยู่แล้ว, ไม่ใส่ API key จริง)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$example = Join-Path $root '.env.example'
$target = Join-Path $root '.env'

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

# 🔄 สุ่ม bytes ด้วย CSPRNG
function New-RandomBytes([int]$n) {
    $b = New-Object byte[] $n
    $rng.GetBytes($b)
    return ,$b
}

# 🔄 สุ่มสตริง a-zA-Z0-9 (ปลอดภัยต่อ connection string)
function New-AlnumSecret([int]$len) {
    $chars = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'
    $sb = New-Object System.Text.StringBuilder
    foreach ($b in (New-RandomBytes $len)) { [void]$sb.Append($chars[$b % $chars.Length]) }
    return $sb.ToString()
}

$values = @{
    'POSTGRES_PASSWORD'    = (New-AlnumSecret 24)
    'JWT_SIGNING_KEY'      = [Convert]::ToBase64String((New-RandomBytes 48))
    'VAULT_ENCRYPTION_KEY' = [Convert]::ToBase64String((New-RandomBytes 32))
    'PII_SERVICE_TOKEN'    = (New-AlnumSecret 40)
    # ⚠️ ผ่าน password policy (ตัวพิมพ์ใหญ่/เล็ก/ตัวเลข/สัญลักษณ์ >= 10)
    'ADMIN_PASSWORD'       = 'Aa1!' + (New-AlnumSecret 16)
    # ⚠️ รหัสผ่านร่วมของ user พนักงาน emp001–emp020 (R7) — ผ่าน password policy
    'EMPLOYEE_SEED_PASSWORD' = 'Aa1!' + (New-AlnumSecret 16)
    'LLM_PROVIDER'         = 'mock'
}

# 🔍 มี .env อยู่แล้ว: ไม่ทับค่าเดิม — ต่อท้ายเฉพาะ key ที่ขาด (ตอนนี้มีแค่ EMPLOYEE_SEED_PASSWORD) แล้วจบ
if (Test-Path $target) {
    $appendOnly = @('EMPLOYEE_SEED_PASSWORD')
    $existing = Get-Content $target -Encoding UTF8
    $added = @()
    foreach ($k in $appendOnly) {
        if (-not ($existing | Where-Object { $_ -match "^$k=" })) {
            $added += "$k=$($values[$k])"
        }
    }
    if ($added.Count -gt 0) {
        # 💾 ต่อท้ายไฟล์ (UTF-8 ไม่มี BOM) — ไม่แก้บรรทัดเดิม
        $utf8Append = New-Object System.Text.UTF8Encoding($false)
        # 🔍 ไฟล์เดิมไม่ลงท้ายด้วย newline → ขึ้นบรรทัดใหม่ก่อน ไม่ให้ต่อท้ายบรรทัดสุดท้าย
        $raw = [System.IO.File]::ReadAllText($target)
        if ($raw.Length -gt 0 -and -not $raw.EndsWith("`n")) { $added = @('') + $added }
        [System.IO.File]::AppendAllLines($target, [string[]]$added, $utf8Append)
        Write-Host "✅ พบ .env อยู่แล้ว — เพิ่มเฉพาะ key ที่ขาด: $(($added | ForEach-Object { $_.Split('=')[0] }) -join ', ') (ไม่แก้ค่าเดิม)"
    } else {
        Write-Host "⚠️ พบ .env อยู่แล้วและมีครบทุก key — ไม่แก้ไข (ลบเองถ้าต้องการสร้างใหม่)"
    }
    exit 0
}

$lines = Get-Content $example -Encoding UTF8 | ForEach-Object {
    $line = $_
    foreach ($k in $values.Keys) {
        if ($line -match "^$k=") { $line = "$k=$($values[$k])" }
    }
    $line
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($target, $lines, $utf8NoBom)
Write-Host "✅ สร้าง .env แล้ว (LLM_PROVIDER=mock) — ADMIN_USERNAME/ADMIN_PASSWORD ดูในไฟล์ .env"
