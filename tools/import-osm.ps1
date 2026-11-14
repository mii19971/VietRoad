# Cap nhat canh bao tu OpenStreetMap (camera, tram thu phi) vao database VietRoad.
# Chay tay:   powershell -ExecutionPolicy Bypass -File D:\VietRoad\tools\import-osm.ps1
# Chay tu dong hang tuan: xem huong dan dang ky Task Scheduler o cuoi file.
#
# Can: .NET SDK, User Secrets da co ConnectionStrings:VietRoadDb, mang ket noi duoc database (VPN).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot                     # thu muc goc D:\VietRoad
$logDir = Join-Path $root "logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir ("import-osm-{0:yyyyMMdd-HHmm}.log" -f (Get-Date))

# Chay importer, ghi log (ca loi) vao file de xem lai khi chay ngam.
dotnet run -c Release --project (Join-Path $root "src\VietRoad.Importer") *>&1 | Tee-Object -FilePath $log
exit $LASTEXITCODE

# ---------------------------------------------------------------------------
# Dang ky chay 3 gio sang Chu nhat hang tuan (mo PowerShell, KHONG can quyen admin):
#
#   $action  = New-ScheduledTaskAction -Execute "powershell.exe" `
#                -Argument "-NoProfile -ExecutionPolicy Bypass -File D:\VietRoad\tools\import-osm.ps1"
#   $trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 3am
#   Register-ScheduledTask -TaskName "VietRoad OSM import" -Action $action -Trigger $trigger
#
# Xoa:  Unregister-ScheduledTask -TaskName "VietRoad OSM import" -Confirm:$false
# Log:  D:\VietRoad\logs\import-osm-*.log
# ---------------------------------------------------------------------------
