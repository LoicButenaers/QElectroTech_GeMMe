param([switch]$Headless, [string]$AssemblyPath = '', [string]$RoundTripProjectPath = '', [string]$ExecutablePath = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$stem = if ($Headless) { 'MCP-headless-demo' } else { 'MCP-demo' }
$hostProcess = $null
function ProcessInfo([string]$mode) {
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    if ($AssemblyPath) { $info.FileName = Join-Path $root 'build/dotnet/dotnet.exe'; $info.ArgumentList.Add($AssemblyPath) }
    elseif ($ExecutablePath) { $info.FileName = $ExecutablePath }
    else { $info.FileName = Join-Path $root 'build/GeMMeElec.exe' }
    $info.ArgumentList.Add($mode)
    if ($Headless) { $info.Environment['GEMMEELEC_TEST_SESSION'] = '1' }
    return $info
}
if ($Headless) {
    $sessionDir = if ($AssemblyPath) { Split-Path $AssemblyPath } elseif ($ExecutablePath) { Split-Path $ExecutablePath } else { Join-Path $root 'build' }
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
    Assert ($list.tools.Count -eq 97) '97 outils déclarés avec schémas'
    $current = Call 'project_get'
    if ($current.dirty -or ($current.project.sheets[0].components.Count -gt 0 -and $current.filePath -ne (Join-Path $root "build/$stem.gemelec"))) { throw 'Ouvrir un projet vierge enregistré avant ce test : le projet existant est préservé.' }
    $null = Call 'project_new' @{ title = 'Essai MCP · circuit de commande' }
    $lib = Call 'library_search' @{ query = 'TeSys' }
    Assert ($lib.equipment.Count -ge 1) 'Recherche catalogue'
    $eplanLibrary = Call 'library_search' @{ library = 'eplan' }
    Assert ($eplanLibrary.equipment.Count -eq 4 -and @($eplanLibrary.equipment | Where-Object canPlace).Count -eq 0) 'EPLAN : aperçus officiels séparés sans bornes inventées'
    $null = Call 'component_add' @{ catalogId = 'eplan-2026-QLTR2_1-1590'; x = 200; y = 200 } -ExpectError
    $official = Call 'component_add' @{ catalogId = 'mfr-schneider-LC1D09BD'; x = 700; y = 300 }
    Assert ($official.reference -eq 'LC1D09BD' -and $official.symbol -eq 'official-schneider-LC1D09BD') 'Fabricant : référence exacte et dessin source'
    $null = Call 'component_delete' @{ id = $official.id }
    $s1 = Call 'component_add' @{ symbol = 'push_nc'; x = 400; y = 200 }
    $s2 = Call 'component_add' @{ symbol = 'push_no'; x = 400; y = 380 }
    $coil = Call 'component_add' @{ symbol = 'coil'; x = 400; y = 600 }
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
    $null = Call 'component_text_update' @{ id=$coil.id; target='tag'; fontSize=24; color='#AA1122'; rotation=30 }
    $styled = ((Call 'project_get').project.sheets[0].components | Where-Object id -eq $coil.id).tagPlacement
    Assert ($styled.fontSize -eq 24 -and $styled.color -eq '#AA1122' -and $styled.rotation -eq 30) 'Style de texte via MCP'
    $null = Call 'component_text_update' @{ id=$coil.id; target='tag'; fontSize=-2 } -ExpectError
    $null = Call 'component_text_update' @{ id=$coil.id; target='tag'; color='incorrect' } -ExpectError
    $styled = ((Call 'project_get').project.sheets[0].components | Where-Object id -eq $coil.id).tagPlacement
    Assert ($styled.fontSize -eq 24 -and $styled.color -eq '#AA1122') 'Styles invalides rejetés sans perte'

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
    Assert (@($symbolLibrary.symbols | Where-Object id -NotLike 'official-*').Count -eq 92 -and @($symbolLibrary.symbols | Where-Object category -like '10*').Count -eq 2) 'Bibliothèque industrielle : 92 symboles et famille des renvois'
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
    $null = Call 'reference_update' @{ id=$arrival.id; displayName='Arrivée automate / été' }
    $renamed = (Call 'reference_list') | Where-Object id -eq $arrival.id
    Assert ($renamed.displayName -eq 'Arrivée automate / été' -and $renamed.linkName -eq '+24V' -and $renamed.targets.Count -eq 1) 'Nom libre sans rupture de liaison'
    Assert (((Call 'reference_list') | Where-Object id -eq $departure.id).displayName -eq 'Arrivée automate / été') 'Renommage automatiquement propagé au report lié'
    $null = Call 'reference_update' @{ id=$arrival.id; displayName='Nom local'; independentName=$true }
    $localRefs = Call 'reference_list'
    Assert (($localRefs | Where-Object id -eq $arrival.id).displayName -eq 'Nom local' -and ($localRefs | Where-Object id -eq $departure.id).displayName -eq 'Arrivée automate / été') 'Nom indépendant sans propagation'
    $null = Call 'reference_update' @{ id=$arrival.id; independentName=$false }
    Assert (((Call 'reference_list') | Where-Object id -eq $arrival.id).displayName -eq 'Arrivée automate / été') 'Retour au nom partagé'
    $referenceTexts = Call 'component_text_list' @{id=$arrival.id}
    Assert (($referenceTexts | Where-Object key -eq 'detail').readOnly -and ($referenceTexts | Where-Object key -eq 'detail').text.Length -gt 0) 'Destination calculée exposée en lecture seule'
    $null = Call 'component_text_update' @{id=$arrival.id;target='detail';text='Fausse destination'} -ExpectError
    $null = Call 'component_text_update' @{id=$arrival.id;target='tag';text='Nom par outil texte'}
    Assert (((Call 'reference_list') | Where-Object id -eq $departure.id).displayName -eq 'Nom par outil texte') 'Édition du texte de report avec propagation'
    $null = Call 'history_undo'
    $null = Call 'component_text_update' @{ id=$arrival.id; target='detail'; fontSize=11; rotation=15; color='#008000'; destinationInline=$true }
    $reportSvg = Join-Path $root 'build/MCP-report-style.svg'
    $null = Call 'export_svg' @{ path=$reportSvg; overwrite=$true }
    [xml]$styledSvg = Get-Content -LiteralPath $reportSvg -Raw
    $green = @($styledSvg.SelectNodes("//*[local-name()='text']") | Where-Object { $_.fill -eq '#008000' -and $_.'font-size' -eq '11' -and $_.transform -like 'rotate(15 *' })
    Assert ($green.Count -eq 1) 'Destination verte et rotation exportées en SVG'
    $null = Call 'component_text_update' @{ id=$arrival.id; target='detail'; side='custom'; offsetX=13.25; offsetY=42.5; horizontal='left'; vertical='top' }
    $renamedComponent = (Call 'project_get').project.sheets[1].components | Where-Object id -eq $arrival.id
    Assert (!$renamedComponent.referenceDestinationInline -and $renamedComponent.detailPlacement.offsetY -eq 42.5) 'Destination du report positionnée indépendamment via MCP'
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
    Assert (((Call 'reference_list') | Where-Object id -eq $arrival.id).displayName -eq 'Arrivée automate / été') 'Nom libre persistant après relecture'
    $savedStyle = ((Call 'project_get').project.sheets[0].components | Where-Object id -eq $coil.id).tagPlacement
    Assert ($savedStyle.fontSize -eq 24 -and $savedStyle.color -eq '#AA1122' -and $savedStyle.rotation -eq 30) 'Styles persistants après relecture'

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
    $fixtureDir = if ($AssemblyPath) { Split-Path $AssemblyPath } elseif ($ExecutablePath) { Split-Path $ExecutablePath } else { Join-Path $root 'build' }
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
    $texts = Call 'component_text_list' @{id=$a.id}
    Assert (@($texts | Where-Object key -like 'port:*').Count -eq 2 -and @($texts | Where-Object key -eq 'tag').Count -eq 1) 'Liste des textes avec clés stables des bornes'
    $null = Call 'component_text_update' @{id=$a.id;target='port:A1';text='Alimentation';offsetX=12.5;color='#112233';fontSize=13;rotation=90}
    $portText = (Call 'component_text_list' @{id=$a.id}) | Where-Object key -eq 'port:A1'
    Assert ($portText.text -eq 'Alimentation' -and $portText.placement.offsetX -eq 12.5 -and $portText.placement.anchorPort -eq 'A1' -and $portText.placement.rotation -eq 90) 'Libellé et style individuels de borne sans changer son ID'
    $addedText = Call 'component_text_add' @{id=$a.id;text='Texte MCP ajouté'}
    Assert ($addedText.key -like 'extra:*') 'Ajout de texte attaché au composant'
    $null = Call 'component_text_update' @{id=$a.id;target=$addedText.key;text='Commande principale';fontSize=17;color='#654321';rotation=15}
    $null = Call 'component_text_align' @{id=$a.id;side='right'}
    $alignedTexts = Call 'component_text_list' @{id=$a.id}
    $extra = $alignedTexts | Where-Object key -eq $addedText.key
    Assert ($extra.placement.side -eq 'custom' -and $extra.placement.offsetX -gt 0 -and $extra.placement.fontSize -eq 17 -and $extra.placement.rotation -eq 15) 'Alignement global conserve le style du texte ajouté'
    $beforeInvalid = (Call 'project_get').project | ConvertTo-Json -Depth 40 -Compress
    $null = Call 'component_text_align' @{id=$a.id;side='invalide'} -ExpectError
    $null = Call 'component_text_update' @{id=$a.id;target='port:absent';text='Erreur'} -ExpectError
    $null = Call 'component_text_delete' @{id=$a.id;target='tag'} -ExpectError
    Assert (((Call 'project_get').project | ConvertTo-Json -Depth 40 -Compress) -eq $beforeInvalid) 'Cibles et alignements invalides rejetés atomiquement'
    $null = Call 'component_text_align' @{id=$a.id;target=$addedText.key;side='bottom'}
    Assert (((Call 'component_text_list' @{id=$a.id}) | Where-Object key -eq $addedText.key).placement.side -eq 'bottom') 'Alignement individuel directionnel'
    $null = Call 'component_text_delete' @{id=$a.id;target=$addedText.key}
    Assert (@((Call 'component_text_list' @{id=$a.id}) | Where-Object key -eq $addedText.key).Count -eq 0) 'Suppression du texte ajouté'
    $null = Call 'history_undo'
    Assert (@((Call 'component_text_list' @{id=$a.id}) | Where-Object key -eq $addedText.key).Count -eq 1) 'Annulation de la suppression de texte'
    $textPath = Join-Path $root 'build/MCP-all-texts.gemelec'
    $null = Call 'project_save' @{path=$textPath;overwrite=$true}
    $null = Call 'project_open' @{path=$textPath}
    $persisted = Call 'component_text_list' @{id=$a.id}
    Assert (($persisted | Where-Object key -eq $addedText.key).text -eq 'Commande principale' -and ($persisted | Where-Object key -eq 'port:A1').text -eq 'Alimentation') 'Textes ajoutés et bornes persistants après relecture'
    $definition = Call 'symbol_get' @{id='coil'}
    $definition.shapes += @{kind='text';x=0;y=0;a=12;b=0;text='Texte interne';rotation=0}
    $null = Call 'symbol_copy' @{definition=$definition;componentId=$a.id}
    $shape = (Call 'component_text_list' @{id=$a.id}) | Where-Object key -like 'shape:*' | Select-Object -Last 1
    $null = Call 'component_text_update' @{id=$a.id;target=$shape.key;text='Texte personnalisé';color='#AABBCC'}
    Assert (((Call 'component_text_list' @{id=$a.id}) | Where-Object key -eq $shape.key).text -eq 'Texte personnalisé') 'Texte interne modifié sur une instance du symbole'
    $null = Call 'project_new' @{title='Bobines et contacts';discardUnsaved=$true}
    $coil = Call 'component_add' @{symbol='coil';x=300;y=300}
    $coilPage = (Call 'project_get').currentSheetId
    $null = Call 'page_add' @{title='Contacts'}
    $no = Call 'component_add' @{symbol='contact_no';x=350;y=300}
    $nc = Call 'component_add' @{symbol='contact_nc';x=650;y=300}
    $wire = Call 'wire_add' @{fromComponent=$no.id;fromPort='14';toComponent=$nc.id;toPort='21'}
    $null = Call 'coil_contact_assign' @{contactId=$no.id;coilId=$coil.id}
    $null = Call 'coil_contact_assign' @{contactId=$nc.id;coilId=$coil.id}
    $functions = Call 'coil_contacts_list'
    Assert ($functions.Count -eq 3 -and ($functions | Where-Object id -eq $coil.id).references -like '*NO*13*14*') 'Associations interfolios et références réciproques MCP'
    $null = Call 'component_update' @{id=$no.id;tag='KM7'}
    $functions = Call 'coil_contacts_list'
    Assert (($functions | Where-Object tag -eq 'KM7').Count -eq 3) 'Repère commun propagé depuis le contact via MCP'
    $errorText = Call 'coil_contact_assign' @{contactId=$coil.id;coilId=$no.id} -ExpectError
    Assert ($errorText.Length -gt 0) 'Association inverse invalide refusée'
    $texts = Call 'component_text_list' @{id=$no.id}
    Assert (($texts | Where-Object key -eq 'coilReferences').readOnly -and ($texts | Where-Object key -eq 'coilReferences').text -like '*1 /*') 'Référence calculée accessible dans les textes MCP'
    $errorText = Call 'component_text_update' @{id=$no.id;target='coilReferences';text='Inventé'} -ExpectError
    Assert ($errorText.Length -gt 0) 'Référence calculée non remplaçable par un texte libre'
    $null = Call 'component_text_update' @{id=$no.id;target='coilReferences';fontSize=14;color='#112233'}
    $contactPath = Join-Path $root "build/$stem-coil-contacts.gemelec"
    $null = Call 'project_save' @{path=$contactPath;overwrite=$true}
    $null = Call 'project_open' @{path=$contactPath}
    Assert ((Call 'coil_contacts_list' | Where-Object coilId -eq $coil.id).Count -eq 2) 'Associations bobine/contacts persistantes après réouverture MCP'
    $null = Call 'page_select' @{id=$coilPage}
    $null = Call 'component_delete' @{id=$coil.id}
    $state = Call 'project_get'
    Assert ($state.project.sheets[1].wires.Count -eq 1 -and ($state.project.sheets[1].components | Where-Object coilId -eq '').Count -eq 2) 'Suppression de bobine préservant contacts et fil via MCP'
    $null = Call 'history_undo' @{}
    Assert ((Call 'coil_contacts_list' | Where-Object coilId -eq $coil.id).Count -eq 2) 'Annulation restaurant les associations via MCP'
    $null = Call 'component_update' @{id=$coil.id;relayFunction=''}
    Assert ((Call 'coil_contacts_list' | Where-Object coilId -eq $coil.id).Count -eq 2) 'Réinitialiser le rôle générique préserve les associations via MCP'
    $errorText = Call 'component_update' @{id=$coil.id;relayFunction='unknown'} -ExpectError
    Assert ($errorText.Length -gt 0 -and (Call 'coil_contacts_list' | Where-Object coilId -eq $coil.id).Count -eq 2) 'Rôle invalide refusé sans perdre les associations MCP'
    $null = Call 'project_new' @{title='Repérage et continuité';discardUnsaved=$true}
    $null = Call 'project_update' @{zoneColumns=24;zoneRows=12}
    $first = (Call 'project_get').currentSheetId
    $exception = Call 'page_add' @{title='Exception'}
    $null = Call 'page_update' @{useProjectZones=$false;zoneColumns=8;zoneRows=6}
    $null = Call 'project_update' @{zoneColumns=16;zoneRows=8}
    $state = Call 'project_get'
    Assert ($state.project.zoneColumns -eq 16 -and $state.project.sheets[0].useProjectZones -and !$state.project.sheets[1].useProjectZones -and $state.project.sheets[1].zoneColumns -eq 8) 'Repérage commun et exception MCP'
    $newPage = Call 'page_add' @{title='Hérite'}
    Assert ($newPage.useProjectZones) 'Nouveau folio héritant du repérage MCP'
    $null = Call 'page_select' @{id=$exception.id}
    $null = Call 'page_update' @{useProjectZones=$true}
    $null = Call 'history_undo' @{}
    Assert (!(Call 'project_get').project.sheets[1].useProjectZones) 'Annulation du retour au repérage commun MCP'
    $errorText = Call 'project_update' @{zoneRows=27} -ExpectError
    Assert ($errorText.Length -gt 0 -and (Call 'project_get').project.zoneRows -eq 8) 'Repérage invalide refusé atomiquement MCP'
    $null = Call 'page_select' @{id=$first}
    $source = Call 'component_add' @{symbol='coil';x=200;y=200}
    $terminal = Call 'component_add' @{symbol='terminal';x=200;y=350}
    $load = Call 'component_add' @{symbol='coil';x=200;y=550}
    $side = Call 'component_add' @{symbol='coil';x=500;y=350}
    $wire = Call 'wire_add' @{fromComponent=$source.id;fromPort='A1';toComponent=$terminal.id;toPort='1';number='26'}
    $continuation = Call 'wire_add' @{fromComponent=$terminal.id;fromPort='2';toComponent=$load.id;toPort='A1'}
    $branch = Call 'wire_add' @{fromComponent=$source.id;fromPort='A1';toComponent=$side.id;toPort='A1'}
    Assert ($continuation.number -eq '26' -and $branch.number -eq '26') 'Même borne et borne de passage gardant le numéro MCP'
    $other = Call 'wire_add' @{fromComponent=$source.id;fromPort='A2';toComponent=$side.id;toPort='A2'}
    Assert ($other.number -ne '26') 'Bornes distinctes du même appareil gardant des numéros distincts MCP'
    $trace = Call 'connection_trace' @{componentId=$terminal.id;portId='1'}
    Assert (@($trace | Where-Object { $_.componentId -eq $terminal.id -and $_.portId -eq '2' }).Count -eq 1 -and @($trace | Where-Object { $_.componentId -eq $source.id -and $_.portId -eq 'A2' }).Count -eq 0) 'Trace incluant le passage sans court-circuiter les autres bornes MCP'
    $null = Call 'wire_update' @{id=$continuation.id;number='24V-01'}
    $state = Call 'project_get'
    Assert (@($state.project.sheets[0].wires | Where-Object number -eq '24V-01').Count -eq 3) 'Renommage de toute la continuité MCP'
    $null = Call 'history_undo' @{}
    Assert (@((Call 'project_get').project.sheets[0].wires | Where-Object number -eq '26').Count -eq 3) 'Renommage des conducteurs annulable en une action MCP'
    $errorText = Call 'wire_update' @{id=$wire.id;fromPort='absente';number='Erreur'} -ExpectError
    Assert ($errorText.Length -gt 0 -and @((Call 'project_get').project.sheets[0].wires | Where-Object number -eq 'Erreur').Count -eq 0) 'Reconnexion invalide préservant tous les numéros MCP'
    $null = Call 'wire_update' @{id=$other.id;number='ALT'}
    $null = Call 'wire_update' @{id=$wire.id;toComponent=$side.id;toPort='A2'}
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].wires | Where-Object id -eq $wire.id).number -eq 'ALT' -and ($state.project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq '26') 'Reconnexion adoptant le numéro de destination sans renommer le réseau détaché MCP'
    $null = Call 'history_undo' @{}
    $continuityPath = Join-Path $root 'build/MCP-zones-continuity.gemelec'
    $null = Call 'project_save' @{path=$continuityPath;overwrite=$true}
    $null = Call 'project_open' @{path=$continuityPath;discardUnsaved=$true}
    $state = Call 'project_get'
    Assert ($state.project.version -eq 10 -and !$state.project.sheets[1].useProjectZones -and @($state.project.sheets[0].wires | Where-Object number -eq '26').Count -eq 3) 'Format courant et réglages relus via MCP'
    $null = Call 'wire_update' @{id=$continuation.id;numberIndependent=$true;number='CABLE-01';color='#D32F2F';thickness=3.2;lineStyle='dashed'}
    $null = Call 'wire_update' @{id=$wire.id;number='27'}
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq 'CABLE-01' -and ($state.project.sheets[0].wires | Where-Object id -eq $branch.id).number -eq '27') 'Numéro indépendant protégé du renommage commun MCP'
    $trace = Call 'connection_trace' @{componentId=$terminal.id;portId='1'}
    Assert (@($trace | Where-Object { $_.componentId -eq $load.id -and $_.portId -eq 'A1' }).Count -eq 1) 'Numéro indépendant conservant la connexion électrique MCP'
    $null = Call 'wire_update' @{id=$continuation.id;number='CABLE-02'}
    Assert (((Call 'project_get').project.sheets[0].wires | Where-Object id -eq $wire.id).number -eq '27') 'Renommage individuel sans propagation MCP'
    $errorText = Call 'wire_update' @{id=$continuation.id;numberIndependent=$false;thickness=99} -ExpectError
    $state = Call 'project_get'
    Assert ($errorText.Length -gt 0 -and ($state.project.sheets[0].wires | Where-Object id -eq $continuation.id).numberIndependent -and ($state.project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq 'CABLE-02') 'Style invalide refusé sans perdre le numéro indépendant MCP'
    $null = Call 'wire_update' @{id=$continuation.id;numberIndependent=$false}
    Assert (((Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq '27') 'Retour au numéro commun MCP'
    $null = Call 'history_undo' @{}
    $styledPath = Join-Path $root 'build/MCP-wire-appearance.gemelec'
    $null = Call 'project_save' @{path=$styledPath;overwrite=$true}
    $null = Call 'project_open' @{path=$styledPath;discardUnsaved=$true}
    $savedWire = (Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id
    Assert ($savedWire.numberIndependent -and $savedWire.number -eq 'CABLE-02' -and $savedWire.thickness -eq 3.2 -and $savedWire.lineStyle -eq 'dashed') 'Numéro indépendant et style relus MCP'
    $wireAppearanceSvgPath = Join-Path $root 'build/MCP-wire-appearance.svg'
    $null = Call 'export_svg' @{path=$wireAppearanceSvgPath;overwrite=$true}
    Assert ((Get-Content -LiteralPath $wireAppearanceSvgPath -Raw).Contains('stroke-dasharray="12.8 9.6"')) 'Export SVG du trait personnalisé MCP'
    $locations = Call 'project_search' @{query='CABLE-02'}
    Assert (@($locations | Where-Object objectId -eq $continuation.id).Count -eq 1) 'Recherche globale retrouvant un fil indépendant MCP'
    $circuitRows = Call 'circuit_read' @{id=$wire.id;highlight=$true}
    Assert (@($circuitRows | Where-Object objectId -eq $continuation.id).Count -eq 1) 'Circuit incluant la borne de passage et le fil indépendant MCP'
    $null = Call 'wire_label_update' @{id=$continuation.id;placement=@{offsetX=44.5;offsetY=-22;fontSize=14;rotation=90;visible=$true;color='#112233'}}
    $placedWire = (Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id
    Assert ($placedWire.numberPlacement.offsetX -eq 44.5 -and $placedWire.numberPlacement.rotation -eq 90) 'Placement précis du numéro de fil MCP'
    $null = Call 'selection_align' @{ids=@($source.id,$terminal.id,$load.id);operation='bottom'}
    $null = Call 'history_undo' @{}
    $errorText = Call 'wire_label_update' @{id=$continuation.id;placement=@{fontSize=999}} -ExpectError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id).numberPlacement.fontSize -eq 14) 'Placement invalide refusé sans perdre le dernier réglage MCP'
    $null = Call 'component_update' @{id=$source.id;tagLocked=$true}
    $rule = @{scope='all';start=10;componentPattern='{prefix}{n:000}';wirePattern='{prefix}{folio}-{n:00}';wirePrefix='W';counterPerFolio=$false}
    $numberPlan = Call 'numbering_preview' @{rules=$rule}
    Assert ($numberPlan.canApply -and @($numberPlan.changes | Where-Object status -eq 'Verrouillé').Count -gt 0) 'Aperçu de numérotation et repère verrouillé MCP'
    $null = Call 'numbering_apply' @{rules=$rule;sourceHash=$numberPlan.sourceHash}
    Assert (((Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq 'CABLE-02') 'Numérotation automatique conservant le fil indépendant MCP'
    $errorText = Call 'numbering_apply' @{rules=$rule;sourceHash=$numberPlan.sourceHash} -ExpectError
    Assert ($errorText.Length -gt 0) 'Aperçu périmé refusé MCP'
    $t2 = Call 'component_add' @{symbol='terminal';x=600;y=350}
    $strip = Call 'terminal_strip_put' @{strip=@{id='strip-mcp';name='X10';location='Armoire';terminals=@(@{componentId=$terminal.id;number='1';level=1;internalPort='1';externalPort='2'},@{componentId=$t2.id;number='2';level=2;internalPort='1';externalPort='2'});bridges=@(@{fromComponent=$terminal.id;fromPort='1';toComponent=$t2.id;toPort='1'})}}
    $trace = Call 'connection_trace' @{componentId=$terminal.id;portId='1'}
    Assert (@($trace | Where-Object { $_.componentId -eq $t2.id -and $_.portId -eq '2' }).Count -eq 1) 'Pontage du bornier participant à la continuité MCP'
    $terminalPages = Call 'terminal_plan_generate' @{id=$strip.id}
    Assert (@($terminalPages).Count -ge 2 -and @((Call 'terminal_strips_list')).Count -eq 1) 'Plan de bornier et pontages paginés MCP'
    $null = Call 'page_select' @{id=$first}
    $cabinet = Call 'cabinet_put' @{cabinet=@{id='cabinet-mcp';name='A1';width=600;height=800;items=@(@{id='item-mcp';kind='device';componentId=$source.id;name='Appareil';x=20;y=20;width=45;height=80;rotation=0;dimensionSource='Saisie utilisateur'})}}
    $cabinetPage = Call 'cabinet_plan_generate' @{id=$cabinet.id}
    Assert ($cabinetPage.cabinetPlanId -eq 'cabinet-mcp' -and @((Call 'cabinets_list')).Count -eq 1) 'Armoire et folios de cotes générés MCP'
    $fiches = Call 'article_fiches_list' @{query='phoenix'}
    Assert (@($fiches).Count -eq 1 -and $fiches.reference -eq '3209510' -and $fiches.width -eq 5.2) 'Fiche exacte sourcée Phoenix Contact MCP'
    $null = Call 'article_fiche_assign' @{componentId=$source.id;ficheId='schneider-LC1D09BD'}
    $state = Call 'project_get'
    Assert (($state.project.sheets[0].components | Where-Object id -eq $source.id).reference -eq 'LC1D09BD' -and $state.project.terminalStrips.Count -eq 1 -and $state.project.cabinets.Count -eq 1) 'Article et données métier persistantes MCP'
    $engineeringPath = Join-Path $root 'build/MCP-engineering.gemelec'
    $null = Call 'project_save' @{path=$engineeringPath;overwrite=$true}
    $null = Call 'project_open' @{path=$engineeringPath;discardUnsaved=$true}
    Assert ((Call 'project_get').project.cabinets[0].items[0].width -eq 45 -and (Call 'project_get').project.terminalStrips[0].bridges.Count -eq 1) 'Armoire et pontages relus via MCP'
    $null = Call 'presentation_apply' @{style=@{name='Noir';symbolColor='#000000';secondaryColor='#000000';wireColor='#000000';preserveIndividual=$true}}
    Assert ((Call 'project_get').project.presentation.name -eq 'Noir' -and ((Call 'project_get').project.sheets[0].wires | Where-Object id -eq $continuation.id).number -eq 'CABLE-02') 'Présentation commune préservant le conducteur indépendant MCP'
    $null = Call 'pdf_options_update' @{contents=$true;bookmarks=$true;links=$true}
    $pdfNavigationPath = Join-Path $root 'build/MCP-navigable.pdf'
    $null = Call 'export_pdf' @{path=$pdfNavigationPath;overwrite=$true}
    Assert ((Get-Content -LiteralPath $pdfNavigationPath -Raw).Contains('/Type /Outlines')) 'Signets PDF exportés MCP'
    $recovery = Call 'recovery_write'
    Assert (@((Call 'recovery_list') | Where-Object file -eq $recovery.file).Count -eq 1) 'Copie de récupération listée MCP'
    $templatePath = Join-Path $root 'build/MCP-template.gemtemplate'
    $null = Call 'template_save' @{path=$templatePath;includeObjects=$true;overwrite=$true}
    $originalId = (Call 'project_get').project.id
    $null = Call 'template_open' @{path=$templatePath;title='Depuis gabarit';discardUnsaved=$true}
    $templateState = Call 'project_get'
    Assert ($templateState.project.id -ne $originalId -and $templateState.project.cabinets.Count -eq 1 -and $templateState.dirty) 'Gabarit indépendant avec implantation MCP'
    $null = Call 'recovery_open' @{path=$recovery.file;discardUnsaved=$true}
    $recoveredState = Call 'project_get'
    Assert ($recoveredState.project.id -eq $originalId -and $recoveredState.filePath -eq $null -and $recoveredState.dirty -and $recoveredState.project.presentation.name -eq 'Noir') 'Récupération sans écraser le fichier de travail MCP'
    $errorText = Rpc 'tools/call' @{name='presentation_apply';arguments=@{style=@{tagSize=1}}} -AllowError
    Assert ($errorText.Length -gt 0 -and (Call 'project_get').project.presentation.name -eq 'Noir') 'Présentation invalide refusée atomiquement MCP'
    $null = Call 'project_new' @{title='Vidéo et nouveaux outils';discardUnsaved=$true}
    for ($i=0; $i -lt 4; $i++) {
        $a = Call 'component_add' @{symbol='terminal';x=(200+100*$i);y=140}
        $b = Call 'component_add' @{symbol='terminal';x=(200+100*$i);y=600}
        $null = Call 'wire_add' @{fromComponent=$a.id;fromPort='2';toComponent=$b.id;toPort='1';number="P$($i+1)"}
    }
    $stripVideo = Call 'terminal_line_create' @{a=@{x=160;y=300};b=@{x=540;y=300};name='X1'}
    Assert ($stripVideo.terminals.Count -eq 4 -and (Call 'project_get').project.sheets[0].wires.Count -eq 8) 'Geste vidéo insère quatre bornes via MCP'
    $cableVideo = Call 'cable_line_create' @{a=@{x=160;y=400};b=@{x=540;y=400};name='W1';sectionMm2=2.5;type='H07V-K'}
    $cableState = Call 'cables_list'
    Assert ($cableState.definitions[0].sectionMm2 -eq 2.5 -and $cableState.assignments.Count -eq 4) 'Ligne câble et section 2,5 mm² MCP'
    $wireVideo = (Call 'project_get').project.sheets[0].wires[0]
    Assert ((Call 'circuit_read' @{id=$wireVideo.id}).Count -eq 2) 'Ligne transversale sans pont électrique MCP'
    $snapshotVideo = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $errorText = Rpc 'tools/call' @{name='cable_put';arguments=@{cable=@{id=$cableVideo.id;name='W1';sectionMm2=-1}}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $snapshotVideo) 'Section négative refusée atomiquement MCP'
    $null = Call 'wire_update' @{id=$wireVideo.id;sectionMm2=1.5}
    Assert ((Call 'project_get').project.sheets[0].wires[0].sectionMm2 -eq 1.5) 'Section individuelle modifiable MCP'
    $null = Call 'wire_update' @{id=$wireVideo.id;clearSection=$true}
    $planVideo = Call 'terminal_plan_generate' @{id=$stripVideo.id;style='graphic'}
    Assert ($planVideo.Count -ge 2 -and $planVideo[0].terminalPlanStyle -eq 'graphic') 'Vue graphique et tableau du bornier MCP'
    $null = Call 'page_select' @{id=(Call 'project_get').project.sheets[0].id}
    $c = Call 'wire_insert_component' @{id=$wireVideo.id;symbol='terminal';x=200;y=230;inputPort='1';outputPort='2'}
    Assert ((Call 'circuit_read' @{id=$wireVideo.id}).Count -eq 3) 'Insertion sur un fil maintient sa continuité MCP'
    $ids = @((Call 'project_get').project.sheets[0].components | ForEach-Object {$_.id})
    $blockPath = Join-Path $root 'build/MCP-new-circuit.gemblock'
    $null = Call 'block_save' @{ids=$ids;name='Bornier quatre fils';category='Borniers';tags='câble';path=$blockPath;overwrite=$true}
    $inserted = Call 'block_insert' @{path=$blockPath;x=750;y=100}
    Assert ($inserted.ids.Count -eq 22 -and (Call 'cables_list').definitions.Count -eq 2) 'Bloc autonome copié avec repères distincts MCP'
    $beforeReading = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $null = Call 'view_reading' @{options=@{textScale=1.5;details=$false;ports=$false;wireNumbers=$false;notes=$false}}
    $null = Call 'view_zoom_selection' @{ids=@($c.id)}
    Assert (((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $beforeReading) 'Lecture et cadrage ne modifient pas le projet MCP'
    $favoriteId = [Guid]::NewGuid().ToString('N')
    $null = Call 'personal_library_put' @{entry=@{id=$favoriteId;name='Favori de test';category='Test';tags='borne';builtinId='terminal'}}
    Assert (@(Call 'personal_library_list' @{query='Favori de test'}).Count -ge 1) 'Favori classé enregistré MCP'
    $placedFavorite = Call 'personal_library_place' @{id=$favoriteId;x=600;y=700}
    Assert ($placedFavorite.ids.Count -eq 1) 'Favori placé MCP'
    $null = Call 'personal_library_delete' @{id=$favoriteId}
    Assert (@(Call 'personal_library_list' @{query='Favori de test'}).Count -eq 0) 'Favori supprimé sans enlever ses placements MCP'
    $originalTab = (Call 'projects_list').activeId
    $newTab = Call 'project_tab_add' @{title='Second onglet'}
    $null = Call 'component_add' @{symbol='terminal';x=200;y=200}
    $errorText = Rpc 'tools/call' @{name='project_tab_close';arguments=@{id=$newTab.id}} -AllowError
    Assert ($errorText.Length -gt 0 -and (Call 'projects_list').projects.Count -eq 2) 'Onglet modifié protégé MCP'
    $null = Call 'project_tab_select' @{id=$originalTab}
    Assert ((Call 'project_get').project.title -eq 'Vidéo et nouveaux outils') 'Projet initial conservé par les onglets MCP'
    $null = Call 'project_tab_close' @{id=$newTab.id;discardUnsaved=$true}
    Assert ((Call 'projects_list').projects.Count -eq 1) 'Fermeture explicite du second onglet MCP'
    $null = Call 'project_new' @{title='Routage MCP';discardUnsaved=$true}
    $a = Call 'component_add' @{symbol='terminal';x=200;y=150}
    $b = Call 'component_add' @{symbol='terminal';x=200;y=650}
    $null = Call 'component_add' @{symbol='coil';x=200;y=400}
    $w = Call 'wire_add' @{fromComponent=$a.id;fromPort='2';toComponent=$b.id;toPort='1'}
    $null = Call 'wire_route_smart' @{ids=@($w.id)}
    Assert ((Call 'project_get').project.sheets[0].wires[0].routingMode -eq 'smart' -and (Call 'project_get').project.sheets[0].wires[0].waypoints.Count -gt 0) 'Routage avec obstacle via MCP'
    $null = Call 'wire_segment_move' @{id=$w.id;segment=2;x=100;y=300}
    Assert ((Call 'project_get').project.sheets[0].wires[0].routingMode -eq 'manual') 'Déplacement de segment conserve le parcours manuel MCP'

    $null = Call 'project_new' @{title='Garde MCP';discardUnsaved=$true}
    $cover = Call 'page_kind' @{kind='cover'}
    Assert ($cover.kind -eq 'cover' -and $cover.titleBlockId -eq '') 'Page de garde sans cartouche par défaut MCP'
    $rect = Call 'graphic_put' @{graphic=@{kind='rect';x=100;y=100;width=500;height=200;color='#203348';fill='#E7EDF3'}}
    $ellipse = Call 'graphic_put' @{graphic=@{kind='ellipse';x=700;y=300;width=200;height=100;color='#007F86';fill='#E7F5F4'}}
    $line = Call 'graphic_put' @{graphic=@{kind='line';x=1100;y=600;width=-900;height=0}}
    $note = Call 'note_put' @{note=@{x=350;y=170;text='{project.title}';fontSize=36;color='#007F86';align='center';resolveFields=$true}}
    Assert ((Call 'project_get').project.sheets[0].graphics.Count -eq 3 -and $note.fontSize -eq 36) 'Rectangles ellipses lignes et texte stylé MCP'
    $image = Call 'graphic_image_import' @{path=(Join-Path $root 'assets/logo.png');x=900;y=100;width=100}
    Assert ($image.kind -eq 'image' -and $image.imageId.Length -gt 0) 'Logo importé sur la page de garde MCP'
    $null = Call 'view_zoom_selection' @{ids=@($rect.id,$note.id)}
    $rect.width = 600
    $null = Call 'graphic_put' @{graphic=$rect}
    Assert ((Call 'project_get').project.sheets[0].graphics[0].width -eq 600) 'Dimensions graphiques modifiables MCP'
    $beforeInvalid = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $errorText = Rpc 'tools/call' @{name='graphic_put';arguments=@{graphic=@{kind='rect';x=100;y=100;width=-1}}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $beforeInvalid) 'Forme invalide refusée atomiquement MCP'
    $coverSvg = Join-Path $root 'build/MCP-cover.svg'
    $null = Call 'export_svg' @{path=$coverSvg;overwrite=$true}
    [xml]$coverXml = Get-Content -LiteralPath $coverSvg -Raw
    $visibleText = @($coverXml.SelectNodes('//*[local-name()="text"]') | ForEach-Object {$_.InnerText})
    Assert ($coverXml.OuterXml.Contains('ellipse') -and $visibleText -contains 'Garde MCP' -and !($visibleText -contains 'Schéma de puissance')) 'SVG de garde sans cartouche et avec champs résolus MCP'
    $null = Call 'graphic_delete' @{id=$ellipse.id}
    Assert ((Call 'project_get').project.sheets[0].graphics.Count -eq 3) 'Suppression de forme MCP'
    $null = Call 'history_undo' @{}
    Assert ((Call 'project_get').project.sheets[0].graphics.Count -eq 4) 'Annulation suppression de forme MCP'
    $withCartouche = Call 'page_kind' @{kind='cover';titleBlockId='technical'}
    Assert ($withCartouche.kind -eq 'cover' -and $withCartouche.titleBlockId -eq 'technical') 'Cartouche facultatif sur garde MCP'
    $coverSaved = Join-Path $root 'build/MCP-cover.gemelec'
    $null = Call 'project_save' @{path=$coverSaved;overwrite=$true}
    $null = Call 'project_open' @{path=$coverSaved}
    Assert ((Call 'project_get').project.version -eq 10 -and (Call 'project_get').project.sheets[0].graphics.Count -eq 4) 'Garde et formes sauvegardées puis relues MCP'

    $null = Call 'project_new' @{title='Placement page';discardUnsaved=$true}
    $null = Call 'page_kind' @{kind='cover'}
    $positionSymbol = Call 'component_add' @{symbol='coil';x=200;y=200}
    $positionTerminal = Call 'component_add' @{symbol='terminal';x=400;y=600}
    $positionWire = Call 'wire_add' @{fromComponent=$positionSymbol.id;fromPort='A2';toComponent=$positionTerminal.id;toPort='1'}
    $null = Call 'element_position' @{id=$positionSymbol.id;axis='X';value=50;unit='percent'}
    Assert ((Call 'project_get').project.sheets[0].components[0].x -eq 700 -and (Call 'project_get').project.sheets[0].wires[0].from.componentId -eq $positionSymbol.id) 'Position symbole en pourcent avec connexion conservée MCP'
    $null = Call 'element_position' @{id=$positionSymbol.id;axis='Y';value=220.5}
    Assert ((Call 'project_get').project.sheets[0].components[0].y -eq 220.5) 'Position numérique précise MCP'
    $positionRect = Call 'graphic_put' @{graphic=@{x=100;y=100;width=200;height=100}}
    $null = Call 'element_position' @{id=$positionRect.id;axis='Width';value=20;unit='percent'}
    Assert ((Call 'project_get').project.sheets[0].graphics[0].width -eq 280) 'Dimensions de forme en pourcent MCP'
    $positionNote = Call 'note_put' @{note=@{x=100;y=400;text='Placement';fontSize=24}}
    $null = Call 'element_position' @{id=$positionNote.id;axis='Y';value=50;unit='percent'}
    Assert ((Call 'project_get').project.sheets[0].notes[0].y -eq 495) 'Position texte en pourcent MCP'
    $positionBefore = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $errorText = Rpc 'tools/call' @{name='element_position';arguments=@{id=$positionRect.id;axis='X';value=110;unit='percent'}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $positionBefore) 'Position hors page refusée atomiquement MCP'
    $null = Call 'selection_align_page' @{ids=@($positionRect.id);operation='center'}
    Assert ((Call 'project_get').project.sheets[0].graphics[0].x -eq 560 -and (Call 'project_get').project.sheets[0].graphics[0].y -eq 445) 'Centrage sur la page entière MCP'
    $null = Call 'history_undo' @{}
    Assert (((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $positionBefore) 'Annulation du centrage page MCP'
    $errorText = Rpc 'tools/call' @{name='element_position';arguments=@{id=$positionRect.id;axis='X';value=10;unit='mm'}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $positionBefore) 'Unité inconnue refusée MCP'

    $null = Call 'project_new' @{title='Tableaux et styles';discardUnsaved=$true}
    $null = Call 'page_kind' @{kind='cover'}
    $richBox = Call 'graphic_put' @{graphic=@{kind='textbox';x=100;y=100;width=350;height=80;content=@{text='Auteur : Bureau';runs=@(@{text='Auteur : ';bold=$true},@{text='Bureau';italic=$true})}}}
    Assert ($richBox.content.runs[0].bold -and $richBox.content.runs[1].italic) 'Rectangle avec texte partiellement gras/italique MCP'
    $richTable = Call 'table_create' @{x=550;y=100;rows=3;columns=4;columnWidth=150;rowHeight=45}
    Assert ($richTable.table.cells.Count -eq 12 -and $richTable.width -eq 600 -and $richTable.height -eq 135) 'Tableau avec grille et tailles initiales MCP'
    $richTable = Call 'table_cell_put' @{id=$richTable.id;row=1;column=2;content=@{text='Texte cellule';bold=$true;italic=$true;fontSize=18;align='center'}}
    Assert ($richTable.table.cells[6].text -eq 'Texte cellule' -and $richTable.table.cells[6].bold -and $richTable.table.cells[6].italic) 'Cellule formatée individuellement MCP'
    $richTable.table.columnWidths = @(100,140,170,190)
    $richTable.table.rowHeights = @(30,45,60)
    $null = Call 'graphic_put' @{graphic=$richTable}
    Assert ((Call 'project_get').project.sheets[0].graphics[1].table.columnWidths[0] -eq 100 -and (Call 'project_get').project.sheets[0].graphics[1].table.rowHeights[2] -eq 60) 'Dimensions individuelles de lignes/colonnes MCP'
    $null = Call 'element_position' @{id=$richTable.id;axis='Width';value=50;unit='percent'}
    Assert ((Call 'project_get').project.sheets[0].graphics[1].width -eq 700 -and [Math]::Abs((Call 'project_get').project.sheets[0].graphics[1].table.columnWidths[0]-116.6666666667) -lt 0.001) 'Redimensionnement proportionnel du tableau en pourcent MCP'
    $richBefore = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $errorText = Rpc 'tools/call' @{name='table_cell_put';arguments=@{id=$richTable.id;row=10;column=2;content=@{text='Hors tableau'}}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $richBefore) 'Cellule hors grille refusée atomiquement MCP'
    $errorText = Rpc 'tools/call' @{name='table_create';arguments=@{x=100;y=400;rows=1.5;columns=2}} -AllowError
    Assert ($errorText.Length -gt 0 -and ((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $richBefore) 'Nombre fractionnaire de lignes refusé MCP'
    $richNote = Call 'note_put' @{note=@{x=100;y=300;text='Note formatée';bold=$true;italic=$true}}
    $richSymbol = Call 'component_add' @{symbol='coil';x=200;y=500}
    $null = Call 'text_style' @{id=$richSymbol.id;textKey='tag';bold=$true;italic=$true}
    Assert ((Call 'project_get').project.sheets[0].notes[0].bold -and (Call 'project_get').project.sheets[0].components[0].tagPlacement.italic) 'Styles des notes et textes de symboles MCP'
    $null = Call 'text_style' @{id=$richBox.id;italic=$true}
    Assert ((Call 'project_get').project.sheets[0].graphics[0].content.runs[0].bold -and (Call 'project_get').project.sheets[0].graphics[0].content.runs[0].italic) 'Italique global conserve le gras partiel MCP'
    $richSvgPath = Join-Path $root 'build/MCP-rich-graphics.svg'
    $null = Call 'export_svg' @{path=$richSvgPath;overwrite=$true}
    $richSvg = Get-Content -LiteralPath $richSvgPath -Raw
    Assert ($richSvg.Contains('font-weight="bold"') -and $richSvg.Contains('font-style="italic"') -and $richSvg.Contains('clipPath')) 'Exports SVG des styles et limites des cellules MCP'
    $richPdfPath = Join-Path $root 'build/MCP-rich-graphics.pdf'
    $null = Call 'export_pdf' @{path=$richPdfPath;overwrite=$true}
    Assert ((Get-Content -LiteralPath $richPdfPath -Raw).Contains('/SegoeUI-Bold-Italic')) 'Police gras italique embarquée dans le PDF MCP'
    $null = Call 'presentation_apply' @{style=@{allTextBold=$true;allTextItalic=$true;preserveIndividual=$true}}
    $null = Call 'export_svg' @{path=$richSvgPath;overwrite=$true}
    [xml]$richGlobalSvg = Get-Content -LiteralPath $richSvgPath -Raw
    Assert (@($richGlobalSvg.SelectNodes('//*[local-name()="text"]') | Where-Object { $_.GetAttribute('font-weight') -ne 'bold' -or $_.GetAttribute('font-style') -ne 'italic' }).Count -eq 0) 'Présentation commune gras/italique sur tous les textes MCP'
    $richSaved = Join-Path $root 'build/MCP-rich-graphics.gemelec'
    $richSnapshot = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
    $null = Call 'project_save' @{path=$richSaved;overwrite=$true}
    $null = Call 'project_open' @{path=$richSaved}
    Assert (((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $richSnapshot -and (Call 'project_get').project.version -eq 10) 'Tableaux et styles relus au format 10 MCP'

    if ($RoundTripProjectPath) {
        if (!$Headless) { throw 'La relecture du document utilisateur exige un hôte de test isolé.' }
        $originalHash = (Get-FileHash -LiteralPath $RoundTripProjectPath -Algorithm SHA256).Hash
        $null = Call 'project_open' @{path=[IO.Path]::GetFullPath($RoundTripProjectPath);discardUnsaved=$true}
        $openedProject = (Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress
        $roundTripPath = Join-Path $root 'build/MCP-user-roundtrip.gemelec'
        $null = Call 'project_save' @{path=$roundTripPath;overwrite=$true}
        $null = Call 'project_open' @{path=$roundTripPath}
        Assert (((Call 'project_get').project | ConvertTo-Json -Depth 100 -Compress) -eq $openedProject) 'Migration du projet utilisateur enregistrée et relue sur copie MCP'
        Assert ((Get-FileHash -LiteralPath $RoundTripProjectPath -Algorithm SHA256).Hash -eq $originalHash) 'Original utilisateur conservé octet pour octet MCP'
    }
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
