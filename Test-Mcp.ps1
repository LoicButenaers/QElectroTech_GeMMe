param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$start = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $root 'build/GeMMeElec.exe'), '--mcp')
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
$start.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
$client = [System.Diagnostics.Process]::Start($start)
$script:requestId = 0
$results = [System.Collections.Generic.List[object]]::new()
function Rpc([string]$method, $parameters) {
    $script:requestId++
    $request = @{ jsonrpc = '2.0'; id = $script:requestId; method = $method; params = $parameters } | ConvertTo-Json -Depth 30 -Compress
    $client.StandardInput.WriteLine($request)
    $task = $client.StandardOutput.ReadLineAsync()
    if (!$task.Wait(25000)) { throw "Délai MCP dépassé : $method" }
    if (!$task.Result) { throw 'Le serveur MCP a fermé stdout.' }
    $reply = $task.Result | ConvertFrom-Json -Depth 40
    if ($reply.id -ne $script:requestId) { throw 'Identifiant JSON-RPC incorrect.' }
    if ($reply.error) { throw ($reply.error | ConvertTo-Json -Compress) }
    return $reply.result
}
function Call([string]$name, $arguments = @{}, [switch]$ExpectError) {
    $result = Rpc 'tools/call' @{ name = $name; arguments = $arguments }
    if ($ExpectError) { if (!$result.isError) { throw "Erreur attendue : $name" }; return $result.content[0].text }
    if ($result.isError) { throw "$name : $($result.content[0].text)" }
    return ($result.content[0].text | ConvertFrom-Json -Depth 40)
}
function Assert([bool]$condition, [string]$name) {
    if (!$condition) { throw "Échec : $name" }
    $results.Add(@{ name = $name; passed = $true })
}
try {
    $init = Rpc 'initialize' @{ protocolVersion = '2025-11-25'; capabilities = @{}; clientInfo = @{ name = 'GeMMeElec integration tests'; version = '1' } }
    Assert ($init.serverInfo.name -eq 'gemmeelec') 'Négociation MCP'
    $client.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $list = Rpc 'tools/list' @{}
    Assert ($list.tools.Count -eq 22) '22 outils déclarés avec schémas'
    $current = Call 'project_get'
    if ($current.dirty -or ($current.project.sheets[0].components.Count -gt 0 -and $current.filePath -ne (Join-Path $root 'build/MCP-demo.gemelec'))) { throw 'Ouvrir un projet vierge enregistré avant ce test : le projet existant est préservé.' }
    $null = Call 'project_new' @{ title = 'Essai MCP · circuit de commande' }
    $lib = Call 'library_search' @{ query = 'TeSys' }
    Assert ($lib.equipment.Count -ge 3) 'Recherche catalogue'
    $s1 = Call 'component_add' @{ symbol = 'push_nc'; x = 400; y = 200 }
    $s2 = Call 'component_add' @{ symbol = 'push_no'; x = 400; y = 380 }
    $coil = Call 'component_add' @{ catalogId = 'f09-1'; x = 400; y = 600 }
    $w1 = Call 'wire_add' @{ fromComponent = $s1.id; fromPort = '22'; toComponent = $s2.id; toPort = '13'; number = '101' }
    $null = Call 'wire_add' @{ fromComponent = $s2.id; fromPort = '14'; toComponent = $coil.id; toPort = 'A1'; number = '102' }
    $null = Call 'component_update' @{ id = $coil.id; tag = 'K1'; rating = '24 V DC à confirmer'; x = 440; y = 600; portLabels = @{ A1 = 'A1+'; A2 = 'A2−' } }
    $state = Call 'project_get'
    Assert ($state.project.sheets[0].components.Count -eq 3 -and $state.project.sheets[0].wires.Count -eq 2) 'Création de symboles et connexions dans la fenêtre'
    Assert (($state.ports | Where-Object componentId -eq $coil.id).terminals[0].x -eq 440) 'Déplacement et bornes absolues'
    $null = Call 'history_undo'
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].components | Where-Object id -eq $coil.id).x -eq 400) 'Annulation commune'
    $null = Call 'history_redo'
    $null = Call 'component_update' @{ id = $coil.id; x = -100 } -ExpectError
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].components | Where-Object id -eq $coil.id).x -eq 440) 'Rejet atomique de coordonnées invalides'
    $null = Call 'wire_add' @{ fromComponent = $s1.id; fromPort = 'absent'; toComponent = $coil.id; toPort = 'A1' } -ExpectError
    $null = Call 'project_new' @{ title = 'Ne doit pas remplacer le projet' } -ExpectError
    $null = Call 'project_get' @{ shell = 'interdit' } -ExpectError
    Assert $true 'Protection du projet non enregistré et paramètres invalides'
    $null = Call 'component_delete' @{ id = $s2.id }
    $state = Call 'project_get'
    Assert ($state.project.sheets[0].wires.Count -eq 0) 'Suppression des connexions incidentes'
    $null = Call 'history_undo'
    $null = Call 'note_add' @{ x = 180; y = 80; text = 'Circuit de commande · essai MCP' }
    $null = Call 'project_validate'
    $firstPage = (Call 'project_get').currentSheetId
    $null = Call 'page_add' @{ title = 'Deuxième folio · réserves' }
    $null = Call 'page_select' @{ id = $firstPage }
    $path = Join-Path $root 'build/MCP-demo.gemelec'
    $null = Call 'project_save' @{ path = $path; overwrite = $true }
    $null = Call 'project_open' @{ path = $path }
    $state = Call 'project_get'
    Assert (!$state.dirty -and $state.project.sheets.Count -eq 2 -and $state.project.sheets[0].wires.Count -eq 2) 'Enregistrement et relecture multipage'
    Assert ($state.project.title -eq 'Essai MCP · circuit de commande' -and $state.project.sheets[1].title -eq 'Deuxième folio · réserves') 'Transport UTF-8 et accents'
    foreach ($format in @('svg','pdf','bom')) {
        $ext = if ($format -eq 'bom') { 'csv' } else { $format }
        $out = Join-Path $root "build/MCP-demo.$ext"
        $null = Call "export_$format" @{ path = $out; overwrite = $true }
        Assert ((Get-Item -LiteralPath $out).Length -gt 100) "Export $ext"
        $null = Call "export_$format" @{ path = $out } -ExpectError
    }
    $null = Call 'view_fit'
    @{ passed = $true; results = $results } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'build/mcp-test.json') -Encoding utf8
    Write-Output "MCP : $($results.Count) vérifications réussies."
}
catch {
    @{ passed = $false; error = $_.ToString(); results = $results } | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'build/mcp-test.json') -Encoding utf8
    throw
}
finally {
    $client.StandardInput.Close()
    if (!$client.WaitForExit(5000)) { $client.Kill() }
    $client.Dispose()
}
