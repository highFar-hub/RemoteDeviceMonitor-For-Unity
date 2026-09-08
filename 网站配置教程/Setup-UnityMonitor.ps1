param(
    [string]$ProjectDirectory = "D:\Unity project\unity-device-monitor-vercel",
    [string]$VercelProjectName = "unity-device-monitor-vercel",
    [string]$DeviceKeyOutputDirectory = ""
)

$ErrorActionPreference = "Stop"

function ConvertFrom-SecureInput {
    param([Security.SecureString]$SecureValue)
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "未找到 $Name。请先安装 Node.js 和 pnpm，并重新打开 PowerShell。"
    }
}

function Set-VercelVariable {
    param(
        [string]$Name,
        [string]$Value,
        [bool]$Sensitive
    )
    Write-Host "正在配置 $Name ..."
    $arguments = @("dlx", "vercel", "env", "add", $Name, "production", "--force", "--token", $script:vercelToken)
    if ($Sensitive) { $arguments += "--sensitive" }
    $Value | & pnpm @arguments
    if ($LASTEXITCODE -ne 0) { throw "配置 $Name 失败。" }
}

Write-Host ""
Write-Host "Unity Remote Monitor - Vercel 配置向导" -ForegroundColor Cyan
Write-Host "脚本不会把 Vercel Token 或 Supabase Secret Key写入项目文件。"
Write-Host ""

Require-Command "node"
Require-Command "pnpm"

if (-not (Test-Path -LiteralPath $ProjectDirectory -PathType Container)) {
    throw "找不到 Vercel 项目目录：$ProjectDirectory"
}

$script:vercelToken = ConvertFrom-SecureInput (Read-Host "粘贴 Vercel Token（vcp_ 开头）" -AsSecureString)
if (-not $script:vercelToken.StartsWith("vcp_")) {
    throw "Vercel Token 格式不正确，应以 vcp_ 开头。"
}

$supabaseUrl = (Read-Host "粘贴 Supabase API URL（https://xxx.supabase.co）").Trim().TrimEnd("/")
$supabaseUrl = $supabaseUrl -replace "/rest/v1$", ""
if ($supabaseUrl -notmatch "^https://[a-zA-Z0-9-]+\.supabase\.co$") {
    throw "Supabase URL 格式不正确，不要包含 /rest/v1/。"
}

$supabaseSecret = ConvertFrom-SecureInput (Read-Host "粘贴 Supabase Secret Key（不要使用 anon key）" -AsSecureString)
if ([string]::IsNullOrWhiteSpace($supabaseSecret)) {
    throw "Supabase Secret Key 不能为空。"
}

$deviceKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
$controlKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")

Set-Location -LiteralPath $ProjectDirectory

Write-Host "正在连接 Vercel 项目..."
& pnpm dlx vercel link --yes --project $VercelProjectName --token $script:vercelToken
if ($LASTEXITCODE -ne 0) { throw "Vercel 项目连接失败。" }

Set-VercelVariable "SUPABASE_URL" $supabaseUrl $false
Set-VercelVariable "SUPABASE_SERVICE_ROLE_KEY" $supabaseSecret $true
Set-VercelVariable "DEVICE_API_KEY" $deviceKey $true
Set-VercelVariable "CONTROL_API_KEY" $controlKey $true

Write-Host "正在部署 Production..."
& pnpm dlx vercel --prod --force --yes --token $script:vercelToken
if ($LASTEXITCODE -ne 0) { throw "Vercel 部署失败。" }

if (-not [string]::IsNullOrWhiteSpace($DeviceKeyOutputDirectory)) {
    if (-not (Test-Path -LiteralPath $DeviceKeyOutputDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $DeviceKeyOutputDirectory | Out-Null
    }
    $keyPath = Join-Path $DeviceKeyOutputDirectory "device-api-key.txt"
    [IO.File]::WriteAllText($keyPath, $deviceKey, (New-Object Text.UTF8Encoding($false)))
    Write-Host "已生成：$keyPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "配置和部署完成。" -ForegroundColor Green
Write-Host "DEVICE_API_KEY（放入 device-api-key.txt）：" -ForegroundColor Yellow
Write-Host $deviceKey
Write-Host "CONTROL_API_KEY（网页控制时输入）：" -ForegroundColor Yellow
Write-Host $controlKey
Write-Host ""
Write-Host "请立即把以上两个值保存到安全位置。关闭窗口后脚本不会替你恢复它们。"
Write-Host "监控地址：https://$VercelProjectName.vercel.app/"

$script:vercelToken = $null
$supabaseSecret = $null
