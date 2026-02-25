param(
    [string]$Project = "LpAutomation.Server/LpAutomation.Server.csproj",
    [string]$Url = "http://localhost:7069",
    [int]$StartupTimeoutSec = 30
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Stop-ServerProcesses {
    Write-Step "Stopping old LpAutomation.Server processes (if any)"
    $procs = Get-CimInstance Win32_Process | Where-Object {
        $_.Name -match "dotnet(\.exe)?$" -and
        $_.CommandLine -match "LpAutomation\.Server\.csproj"
    }

    if (-not $procs) {
        Write-Host "No prior server process found."
        return
    }

    foreach ($p in $procs) {
        try {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop
            Write-Host "Stopped PID $($p.ProcessId)"
        } catch {
            Write-Warning "Could not stop PID $($p.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Wait-ForEndpoint([string]$endpoint, [int]$timeoutSec) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
        try {
            $resp = Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 3
            if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) {
                return $true
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

Write-Step "Preflight"
dotnet --info | Out-Host

Stop-ServerProcesses

Write-Step "Starting server"
$runArgs = @(
    "run",
    "--project", $Project,
    "--urls", $Url
)

# Start detached process so script can continue
$proc = Start-Process -FilePath "dotnet" -ArgumentList $runArgs -PassThru -WindowStyle Hidden
Write-Host "Started dotnet PID: $($proc.Id)"

try {
    $swaggerJsonUrl = "$Url/swagger/v1/swagger.json"
    $swaggerUiUrl   = "$Url/swagger"
    $redocUrl       = "$Url/redoc"

    Write-Step "Waiting for API startup"
    $ready = Wait-ForEndpoint -endpoint $swaggerJsonUrl -timeoutSec $StartupTimeoutSec
    if (-not $ready) {
        throw "Server did not become ready within $StartupTimeoutSec seconds at $swaggerJsonUrl"
    }

    Write-Step "Fetching OpenAPI document"
    $raw = Invoke-WebRequest -Uri $swaggerJsonUrl -UseBasicParsing -TimeoutSec 10
    Write-Host "HTTP $($raw.StatusCode) from $swaggerJsonUrl"

    $json = $raw.Content | ConvertFrom-Json

    $hasOpenApi = $null -ne $json.openapi -and ($json.openapi -match '^3\.\d+\.\d+$')
    $hasSwagger2 = $null -ne $json.swagger -and ($json.swagger -eq "2.0")

    if (-not ($hasOpenApi -or $hasSwagger2)) {
        throw "OpenAPI version field missing/invalid. Expected openapi: 3.x.y or swagger: 2.0."
    }

    Write-Host "Spec version OK." -ForegroundColor Green
    if ($hasOpenApi)  { Write-Host "openapi = $($json.openapi)" }
    if ($hasSwagger2) { Write-Host "swagger = $($json.swagger)" }

    Write-Step "Endpoints you can now open in browser"
    Write-Host $swaggerUiUrl
    Write-Host $redocUrl

    Write-Step "Result"
    Write-Host "PASS: Swagger JSON and version field are valid." -ForegroundColor Green
}
catch {
    Write-Step "Result"
    Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Write-Step "Stopping test server process"
    try {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force
            Write-Host "Stopped PID $($proc.Id)"
        }
    } catch {
        Write-Warning "Could not stop process PID $($proc.Id): $($_.Exception.Message)"
    }
}
