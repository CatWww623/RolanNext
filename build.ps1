param([switch]$Pack, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\portable\RolanNext'))
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$privateSdk = Join-Path $taskRoot '.tools\dotnet-fast\dotnet.exe'
$dotnetExe = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnetExe publish (Join-Path $taskRoot 'src\Rolan.App\Rolan.App.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw '发布失败' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'README.md') -Destination (Join-Path $OutputDirectory 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\Rolan.Core\Unicode-LICENSE.txt') -Destination (Join-Path $OutputDirectory 'Unicode-LICENSE.txt') -Force
if ($Pack)
{
    New-Item -ItemType Directory -Path (Join-Path $taskRoot 'artifacts') -Force | Out-Null
    $archive = Join-Path $taskRoot ('artifacts\RolanNext-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
    Compress-Archive -LiteralPath $OutputDirectory -DestinationPath $archive -CompressionLevel Optimal
    Write-Output $archive
}
