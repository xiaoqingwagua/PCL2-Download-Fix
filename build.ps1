# 构建单文件修复工具，产物在 out\PCL下载修复工具.exe
$ErrorActionPreference = 'Stop'
Push-Location "$PSScriptRoot\src\PCLDownloadFix"
try { dotnet publish -c Release -o "$PSScriptRoot\out" } finally { Pop-Location }
Remove-Item "$PSScriptRoot\out\*.config" -ErrorAction SilentlyContinue
Get-FileHash "$PSScriptRoot\out\PCL下载修复工具.exe" -Algorithm SHA256
