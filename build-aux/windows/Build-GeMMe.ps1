param(
    [ValidateRange(1, 12)]
    [int]$Jobs = 3
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildPath = Join-Path $projectRoot 'build'
$bashPath = Join-Path $buildPath 'msys64\usr\bin\bash.exe'

if (-not (Test-Path -LiteralPath $bashPath)) {
    throw 'La chaîne MSYS2 UCRT64 doit être installée dans build\msys64. Voir README.md.'
}

Push-Location $projectRoot
$previousMsystem = $env:MSYSTEM
$previousChere = $env:CHERE_INVOKING
$previousJobs = $env:GEMME_BUILD_JOBS
try {
    $branch = & git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -notin @('main', 'EPLAN_DESIGN')) {
        throw 'La compilation GeMMe doit être effectuée sur main ou EPLAN_DESIGN.'
    }
    $running = Get-Process -Name qelectrotech -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq (Join-Path $buildPath 'qelectrotech.exe') }
    if ($running) {
        throw 'Fermer QElectroTech avant de remplacer son exécutable.'
    }

    $env:MSYSTEM = 'UCRT64'
    $env:CHERE_INVOKING = '1'
    $env:GEMME_BUILD_JOBS = "$Jobs"
    $buildCommand = @'
set -euo pipefail
export CCACHE_DIR="$PWD/build/ccache"
ccache --set-config=max_size=500M
cmake -S . -B build -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_PREFIX_PATH=/ucrt64 \
  -DQt6_DIR=/ucrt64/lib/cmake/Qt6 \
  -DBUILD_WITH_KF=ON -DBUILD_KF=OFF -DPACKAGE_TESTS=OFF \
  -DCMAKE_POLICY_DEFAULT_CMP0077=NEW -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
  -DQET_EXPORT_PROJECT_DB=ON \
  -DQET_ENABLE_PCH=ON \
  -DQET_ENABLE_SPACEMOUSE=ON -DQET_SPACEMOUSE_BACKEND=hid \
  -DCMAKE_C_COMPILER_LAUNCHER=/ucrt64/bin/ccache \
  -DCMAKE_CXX_COMPILER_LAUNCHER=/ucrt64/bin/ccache \
  -DSQLite3_INCLUDE_DIR=/ucrt64/include \
  -DSQLite3_LIBRARY=/ucrt64/lib/libsqlite3.dll.a
cmake --build build --parallel "$GEMME_BUILD_JOBS"
cd build
windeployqt6 --release --no-quick-import --skip-plugin-types qmltooling \
  --translations fr --translationdir lang --no-compiler-runtime qelectrotech.exe
# Deploy every transitive UCRT64 dependency, including Qt plugin dependencies.
# Only the application folder and its plugin subfolders are scanned, not MSYS2.
while :; do
  copied=0
  while IFS= read -r -d '' binary; do
    dependencies=$(ldd "$binary")
    while IFS= read -r dll; do
      [ -f "$dll" ] || continue
      name=$(basename "$dll")
      if [ ! -f "$name" ]; then
        cp "$dll" "$name"
        copied=1
      fi
    done < <(printf '%s\n' "$dependencies" | awk '$3 ~ /^\/ucrt64\/bin\// {print $3}')
  done < <(find . -maxdepth 2 -type f \( -iname '*.exe' -o -iname '*.dll' \) -print0)
  [ "$copied" -eq 0 ] && break
done
test -s qelectrotech.exe
test -f platforms/qwindows.dll
test -f libhidapi-0.dll
pacman -Q > toolchain-packages.txt
'@
    & $bashPath -lc $buildCommand 2>&1 | Tee-Object -FilePath (Join-Path $buildPath 'compilation.log')
    if ($LASTEXITCODE -ne 0) {
        throw "Échec de compilation ou de déploiement (code $LASTEXITCODE). Voir build\compilation.log."
    }

    # Reuse tracked resources through junctions instead of duplicating them.
    foreach ($resource in @('elements', 'titleblocks', 'examples')) {
        $resourcePath = Join-Path $projectRoot $resource
        $linkPath = Join-Path $buildPath $resource
        if (-not (Test-Path -LiteralPath $linkPath)) {
            New-Item -ItemType Junction -Path $linkPath -Target $resourcePath | Out-Null
        }
    }
    Write-Host "Exécutable disponible : $(Join-Path $buildPath 'qelectrotech.exe')"
}
finally {
    $env:MSYSTEM = $previousMsystem
    $env:CHERE_INVOKING = $previousChere
    $env:GEMME_BUILD_JOBS = $previousJobs
    Pop-Location
}
