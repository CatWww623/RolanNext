param([string]$LegacyConfig)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$privateSdk = Join-Path $taskRoot '.tools\dotnet-fast\dotnet.exe'
$dotnetExe = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnetExe build (Join-Path $taskRoot 'src\Rolan.App\Rolan.App.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '编译验证失败' }
$coreArguments = @($taskRoot)
if ($LegacyConfig) { $coreArguments += (Resolve-Path -LiteralPath $LegacyConfig).Path }
& $dotnetExe run --project (Join-Path $taskRoot 'tests\Rolan.Tests\Rolan.Tests.csproj') -- @coreArguments
if ($LASTEXITCODE -ne 0) { throw '核心数据验证失败' }
& $dotnetExe run --project (Join-Path $taskRoot 'tests\Rolan.WindowsTests\Rolan.WindowsTests.csproj') -- $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Windows集成验证失败' }
