param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $root 'build/dotnet/dotnet.exe'
if (!(Test-Path $dotnet)) {
 New-Item -ItemType Directory -Path (Join-Path $root 'build') -Force | Out-Null
 $installer = Join-Path $root 'build/dotnet-install.ps1'
 Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
 & $installer -Version '10.0.401' -InstallDir (Join-Path $root 'build/dotnet') -NoPath
 if (!(Test-Path $dotnet)) { throw 'Installation locale du SDK .NET 10 échouée.' }
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $root 'build/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $root 'build/packages'
& $dotnet publish (Join-Path $root 'GeMMeElec.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $root 'build') -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée.' }
if ($Test) {
 $process = Start-Process -FilePath (Join-Path $root 'build/GeMMeElec.exe') -ArgumentList '--self-test' -WorkingDirectory $root -WindowStyle Hidden -PassThru -Wait
 if ($process.ExitCode -ne 0) { throw 'Tests échoués. Voir build/self-test.json.' }
}
Write-Output "Application : $root\build\GeMMeElec.exe"
