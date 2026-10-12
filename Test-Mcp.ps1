param([switch]$Headless, [string]$AssemblyPath = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$stem = if ($Headless) { 'MCP-headless-demo' } else { 'MCP-demo' }
$hostProcess = $null
function ProcessInfo([string]$mode) {
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    if ($AssemblyPath) { $info.FileName = Join-Path $root 'build/dotnet/dotnet.exe'; $info.ArgumentList.Add($AssemblyPath) }
    else { $info.FileName = Join-Path $root 'build/GeMMeElec.exe' }
    $info.ArgumentList.Add($mode)
    if ($Headless) { $info.Environment['GEMMEELEC_TEST_SESSION'] = '1' }
    return $info
}
if ($Headless) {
    $sessionDir = if ($AssemblyPath) { Split-Path $AssemblyPath } else { Join-Path $root 'build' }
    $sessionFile = Join-Path $sessionDir 'GeMMeElec.test-session.json'
    $hostProcess = [System.Diagnostics.Process]::Start((ProcessInfo '--test-host'))
    $ready = $false
    for ($i=0; $i -lt 100; $i++) {
        if ($hostProcess.HasExited) { throw 'Hôte MCP de test arrêté au démarrage.' }
        if (Test-Path $sessionFile) { try { $session = Get-Content -Raw $sessionFile | ConvertFrom-Json; if ($session.processId -eq $hostProcess.Id) { $ready = $true; break } } catch {} }
        Start-Sleep -Milliseconds 100
    }
    if (!$ready) { $hostProcess.Kill(); throw 'Hôte MCP de test indisponible.' }
}
$start = ProcessInfo '--mcp'
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
    Assert ($list.tools.Count -eq 41) '41 outils déclarés avec schémas'
    $current = Call 'project_get'
    if ($current.dirty -or ($current.project.sheets[0].components.Count -gt 0 -and $current.filePath -ne (Join-Path $root "build/$stem.gemelec"))) { throw 'Ouvrir un projet vierge enregistré avant ce test : le projet existant est préservé.' }
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
    $null = Call 'wire_update' @{ id = $w1.id; waypoints = @(@{ x = 320; y = 250 }, @{ x = 320; y = 330 }) }
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].wires | Where-Object id -eq $w1.id).waypoints.Count -eq 2) 'Édition du tracé des conducteurs'
    $null = Call 'component_text_update' @{ id = $coil.id; target = 'tag'; side = 'top'; horizontal = 'center'; vertical = 'bottom'; offsetX = 12.5; offsetY = -8.25 }
    $null = Call 'component_text_update' @{ id = $coil.id; target = 'tag'; side = 'incorrect' } -ExpectError
    $null = Call 'project_validate'
    $firstPage = (Call 'project_get').currentSheetId
    $null = Call 'page_add' @{ title = 'Deuxième folio · réserves' }
    $null = Call 'page_select' @{ id = $firstPage }
    $null = Call 'project_update' @{ author = 'GeMMe'; customer = 'Atelier'; documentNumber = 'ELEC-2026'; customFields = @{ affaire = 'BE-001' } }
    $null = Call 'page_update' @{ number = '01-A'; checkedBy = 'Vérificateur'; titleBlockId = 'technical'; customFields = @{ armoire = 'AR1' } }
    $templates = Call 'titleblock_list'
    Assert ($templates.values.'sheet.author' -eq 'GeMMe' -and $templates.values.'sheet.number' -eq '01-A' -and $templates.values.'sheet.custom.armoire' -eq 'AR1') 'Métadonnées et héritage des champs de cartouche'
    $template = @{ id = 'integration'; name = 'Essai technique'; height = 140; cells = @(@{ x=0; y=0; width=100; height=100; text='{project.title} / {sheet.number}'; align='center'; vertical='center'; fontSize=18; border=$true; imageId='' }) }
    $null = Call 'titleblock_put' @{ template = $template }
    $null = Call 'page_update' @{ titleBlockId = 'integration' }
    $template.cells[0].width = 101
    $null = Call 'titleblock_put' @{ template = $template } -ExpectError
    Assert (((Call 'titleblock_list').templates | Where-Object id -eq 'integration').cells[0].width -eq 100) 'Cartouche éditable et rejet atomique des cellules hors limites'
    $null = Call 'page_move' @{ index = 2 }
    Assert ((Call 'titleblock_list').values.'sheet.index' -eq '2') 'Ordre des folios et numéro explicite indépendant'
    $null = Call 'page_move' @{ index = 1 }
    $image = Call 'component_image' @{ id=$coil.id; path=(Join-Path $root 'assets/logo.png') }
    Assert ($image.imageId.Length -gt 0) 'Image intégrée au composant'
    $null = Call 'bom_configure' @{ options = @{ columns = @(@{ key='image'; label='Photo / symbole'; enabled=$true }, @{ key='quantity'; label='Qté'; enabled=$true }, @{ key='reference'; label='Référence'; enabled=$true }); groupArticles=$true; includeAuxiliary=$false; sheetId=''; brand='' } }
    $htmlPath = Join-Path $root "build/$stem.html"
    $null = Call 'export_bom_html' @{ path=$htmlPath; overwrite=$true }
    Assert ((Get-Content -Raw $htmlPath).Contains('data:image/jpeg;base64,') -and (Get-Content -Raw $htmlPath).Contains('<svg')) 'BOM HTML avec photo intégrée et symboles de remplacement'
    $null = Call 'export_bom_html' @{ path=$htmlPath } -ExpectError
    $path = Join-Path $root "build/$stem.gemelec"
    $null = Call 'project_save' @{ path = $path; overwrite = $true }
    $null = Call 'project_open' @{ path = $path }
    $state = Call 'project_get'
    Assert (!$state.dirty -and $state.project.sheets.Count -eq 2 -and $state.project.sheets[0].wires.Count -eq 2) 'Enregistrement et relecture multipage'
    Assert ($state.project.title -eq 'Essai MCP · circuit de commande' -and $state.project.sheets[1].title -eq 'Deuxième folio · réserves') 'Transport UTF-8 et accents'
    $placement = ($state.project.sheets[0].components | Where-Object id -eq $coil.id).tagPlacement
    Assert ($placement.side -eq 'top' -and $placement.offsetX -eq 12.5 -and $placement.offsetY -eq -8.25) 'Positions de texte précises conservées après relecture'
    Assert ($state.project.sheets[0].number -eq '01-A' -and $state.project.bomOptions.columns[0].key -eq 'image' -and $state.project.images.PSObject.Properties.Count -gt 0) 'Cartouches, options BOM et images conservés après relecture'
    foreach ($format in @('svg','pdf','bom')) {
        $ext = if ($format -eq 'bom') { 'csv' } else { $format }
        $out = Join-Path $root "build/$stem.$ext"
        $null = Call "export_$format" @{ path = $out; overwrite = $true }
        Assert ((Get-Item -LiteralPath $out).Length -gt 100) "Export $ext"
        $null = Call "export_$format" @{ path = $out } -ExpectError
    }
    $null = Call 'view_fit'
    $null = Call 'wire_branch' @{ fromComponent=$coil.id; fromPort='A2'; targetWire=$w1.id; x=320; y=290 }
    $state = Call 'project_get'
    Assert ($state.project.sheets[0].wires.Count -eq 4 -and @($state.project.sheets[0].components | Where-Object symbol -eq 'junction').Count -eq 1) 'Dérivation réelle sur le trajet du conducteur'
    $null = Call 'history_undo'
    Assert ((Call 'project_get').project.sheets[0].wires.Count -eq 2) 'Annulation atomique de la dérivation'
    $null = Call 'history_redo'
    $null = Call 'bom_generate'
    $state = Call 'project_get'
    Assert ($state.project.sheets.Count -eq 3 -and $state.project.sheets[2].titleBlockId -eq '' -and $null -ne $state.project.sheets[2].bomPage) 'Folio BOM intégré sans cartouche'
    $null = Call 'component_add' @{ symbol='coil'; x=200; y=200 } -ExpectError
    $null = Call 'bom_generate'
    Assert ((Call 'project_get').project.sheets.Count -eq 3) 'Actualisation BOM sans duplication des folios'
    $null = Call 'project_save' @{ path=$path; overwrite=$true }
    $null = Call 'project_open' @{ path=$path }
    $state = Call 'project_get'
    Assert ($state.project.sheets[0].wires.Count -eq 4 -and $state.project.sheets[2].bomPage.rows.Count -gt 0) 'Dérivation et nomenclature conservées après relecture'
    $null = Call 'export_pdf' @{ path=(Join-Path $root "build/$stem.pdf"); overwrite=$true }
    $symbolLibrary = Call 'library_search'
    Assert ($symbolLibrary.symbols.Count -eq 92 -and @($symbolLibrary.symbols | Where-Object category -like '10*').Count -eq 2) 'Bibliothèque industrielle : 92 symboles et famille des renvois'
    $null = Call 'page_select' @{ id=$firstPage }
    $departure = Call 'component_add' @{ symbol='folio_next'; x=800; y=700 }
    $null = Call 'wire_add' @{ fromComponent=$coil.id; fromPort='A2'; toComponent=$departure.id; toPort='1' }
    $null = Call 'reference_update' @{ id=$departure.id; name='+24V'; mode='pair' }
    $secondPage = $state.project.sheets[1].id
    $null = Call 'page_select' @{ id=$secondPage }
    $arrival = Call 'component_add' @{ symbol='folio_previous'; x=200; y=300 }
    $lamp = Call 'component_add' @{ symbol='lamp'; x=600; y=300 }
    $null = Call 'wire_add' @{ fromComponent=$arrival.id; fromPort='1'; toComponent=$lamp.id; toPort='X1' }
    $null = Call 'reference_update' @{ id=$arrival.id; name='+24V'; mode='pair' }
    $refs = Call 'reference_list'
    Assert ($refs.Count -eq 2 -and ($refs | Where-Object id -eq $departure.id).targets[0].address -eq '2 / D3') 'Renvois réciproques avec emplacement du folio'
    $trace = Call 'connection_trace' @{ componentId=$coil.id; portId='A2' }
    Assert (@($trace | Where-Object { $_.componentId -eq $lamp.id -and $_.portId -eq 'X1' }).Count -eq 1 -and @($trace | Where-Object { $_.componentId -eq $lamp.id -and $_.portId -eq 'X2' }).Count -eq 0) 'Continuité interfolio sans traverser les appareils'
    $null = Call 'page_update' @{ number='CMD-2'; zoneColumns=16; zoneRows=8 }
    $null = Call 'page_update' @{ zoneColumns=0 } -ExpectError
    Assert (((Call 'reference_list') | Where-Object id -eq $departure.id).targets[0].address -eq 'CMD-2 / C3') 'Repérage configurable et rejet des valeurs invalides'
    $null = Call 'component_delete' @{ id=$arrival.id }
    Assert (((Call 'reference_list') | Where-Object id -eq $departure.id).targets.Count -eq 0) 'Détection de renvoi cassé'
    $null = Call 'history_undo'
    $null = Call 'bom_configure' @{ options=@{ columns=@(@{key='tags';label='Repères';enabled=$true},@{key='pages';label='Folios / zones';enabled=$true}); groupArticles=$true; includeAuxiliary=$false; sheetId=''; brand='' } }
    $null = Call 'bom_generate'
    $null = Call 'project_save' @{ path=$path; overwrite=$true }
    $null = Call 'project_open' @{ path=$path }
    $trace = Call 'connection_trace' @{ componentId=$coil.id; portId='A2' }
    Assert (@($trace | Where-Object componentId -eq $lamp.id).Count -eq 1) 'Renvois persistants après relecture'
    $state = Call 'project_get'
    $bomText = $state.project.sheets[-1].bomPage.rows | ConvertTo-Json -Depth 20
    Assert ($bomText.Contains('CMD-2') -and $bomText.Contains('C7')) 'Nomenclature avec position des composants'
    $null = Call 'export_pdf' @{ path=(Join-Path $root "build/$stem.pdf"); overwrite=$true }
    $null = Call 'page_select' @{ id=$firstPage }
    $definition = Call 'symbol_get' @{ id='coil' }
    $definition.name = 'Bobine MCP personnelle'
    $custom = Call 'symbol_copy' @{ definition=$definition; componentId=$coil.id }
    Assert ($custom.id -like 'custom-*' -and (Call 'symbol_get' @{id='coil'}).name -ne $custom.name) 'Copie de symbole sans modifier la source'
    $state = Call 'project_get'
    Assert (@($state.project.sheets[0].components | Where-Object symbol -eq $custom.id).Count -eq 1 -and $state.project.sheets[0].wires.Count -gt 0) 'Remplacement du symbole avec connexions conservées'
    $symbolPath = Join-Path $root 'build/MCP-custom-symbol.gemsymbol'
    $null = Call 'symbol_export' @{ id=$custom.id; path=$symbolPath; overwrite=$true }
    $null = Call 'symbol_export' @{ id=$custom.id; path=$symbolPath } -ExpectError
    $importedSymbol = Call 'symbol_import' @{ path=$symbolPath }
    Assert ($importedSymbol.symbol.id -ne $custom.id -and $importedSymbol.symbol.ports.Count -eq 2) 'Échange de symbole autonome et protection du fichier existant'
    $null = Call 'project_save' @{ path=$path; overwrite=$true }
    $null = Call 'project_open' @{ path=$path }
    Assert ((Call 'symbol_get' @{id=$custom.id}).name -eq 'Bobine MCP personnelle') 'Bibliothèque personnelle persistante'
    $fixtureDir = if ($AssemblyPath) { Split-Path $AssemblyPath } else { Join-Path $root 'build' }
    $qetPath = Join-Path $fixtureDir 'qet-import-fixture.qet'
    $report = Call 'project_import' @{ path=$qetPath }
    Assert ($report.sheets -eq 1 -and !$report.applied -and (Call 'project_get').project.title -eq 'Essai MCP · circuit de commande') 'Analyse de conversion sans remplacer le projet courant'
    $null = Call 'project_import' @{ path=$qetPath; apply=$true }
    $state = Call 'project_get'
    Assert ($state.dirty -and !$state.filePath -and $state.project.sheets[0].wires.Count -eq 1) 'Import QET ouvert comme nouveau projet non enregistré'
    $dxfPath = Join-Path $fixtureDir 'manufacturer-symbol-fixture.dxf'
    $null = Call 'project_import' @{ path=$dxfPath; apply=$true } -ExpectError
    $dxf = Call 'symbol_import' @{ path=$dxfPath; scale=2 }
    Assert ($dxf.symbol.shapes.Count -gt 40 -and $dxf.symbol.ports.Count -eq 0 -and $dxf.warnings.Count -gt 0) 'Import DXF sans bornes électriques inventées'
    $dxfReport = Call 'project_import' @{ path=$dxfPath }
    Assert ($dxfReport.report.Contains('graphique') -and !$dxfReport.applied) 'Limites de conversion EPLAN DXF explicites'
    $null = Call 'project_import' @{ path=(Join-Path $root 'build/project.zw1') } -ExpectError
    $null = Call 'project_new' @{ title='Test déplacement de jonction'; discardUnsaved=$true }
    $a = Call 'component_add' @{symbol='coil';x=200;y=150}; $b = Call 'component_add' @{symbol='coil';x=200;y=600}; $c = Call 'component_add' @{symbol='coil';x=600;y=300}
    $wire = Call 'wire_add' @{fromComponent=$a.id;fromPort='A2';toComponent=$b.id;toPort='A1'}
    $branch = Call 'wire_branch' @{fromComponent=$c.id;fromPort='A1';targetWire=$wire.id;x=200;y=350}
    $null = Call 'wire_segment_move' @{id=$wire.id;segment=1;x=250;y=350}
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].components | Where-Object autoJunction).x -eq 250) 'Jonction déplaçable avec le segment via MCP'
    $null = Call 'wire_delete' @{id=$branch.id}
    Assert (@((Call 'project_get').project.sheets[0].components | Where-Object autoJunction).Count -eq 0) 'Nettoyage automatique après suppression de branche'
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
    if ($hostProcess) { if (!$hostProcess.HasExited) { $hostProcess.Kill(); $hostProcess.WaitForExit() }; $hostProcess.Dispose() }
}
