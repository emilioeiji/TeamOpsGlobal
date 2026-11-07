[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppDirectory,

    [string]$ConfigPath,

    [switch]$TestWrite,

    [switch]$AllowLocalStorageRoot
)

$ErrorActionPreference = 'Stop'
$script:Failures = 0
$script:Warnings = 0

function Write-Check {
    param(
        [ValidateSet('PASS', 'WARN', 'FAIL')]
        [string]$Status,
        [string]$Message
    )

    switch ($Status) {
        'PASS' { Write-Host "[PASS] $Message" -ForegroundColor Green }
        'WARN' {
            $script:Warnings++
            Write-Host "[WARN] $Message" -ForegroundColor Yellow
        }
        'FAIL' {
            $script:Failures++
            Write-Host "[FAIL] $Message" -ForegroundColor Red
        }
    }
}

function Get-FullConfiguredPath {
    param(
        [string]$Value,
        [string]$StorageRoot,
        [string]$ApplicationRoot,
        [switch]$RelativeToApplication
    )

    $expanded = [Environment]::ExpandEnvironmentVariables($Value.Trim())
    if ($expanded -match '^[A-Za-z]:(?![\\/])') {
        throw "Caminho relativo a unidade nao permitido: $expanded"
    }

    if ([IO.Path]::IsPathRooted($expanded)) {
        return [IO.Path]::GetFullPath($expanded).TrimEnd('\', '/')
    }

    $basePath = if ($RelativeToApplication) { $ApplicationRoot } else { $StorageRoot }
    $resolved = [IO.Path]::GetFullPath([IO.Path]::Combine($basePath, $expanded)).TrimEnd('\', '/')
    if (-not $RelativeToApplication) {
        $normalizedRoot = [IO.Path]::GetFullPath($StorageRoot).TrimEnd('\', '/') + '\'
        if (-not ($resolved + '\').StartsWith($normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Caminho sai de StorageRoot: $resolved"
        }
    }

    return $resolved
}

function Test-ConfiguredLocation {
    param(
        [string]$Key,
        [string]$Path,
        [ValidateSet('File', 'Directory')]
        [string]$Kind,
        [switch]$Optional,
        [switch]$CreatedOnDemand
    )

    $exists = if ($Kind -eq 'File') { Test-Path -LiteralPath $Path -PathType Leaf } else { Test-Path -LiteralPath $Path -PathType Container }
    if ($exists) {
        Write-Check PASS "$Key disponivel: $Path"
        return
    }

    if ($CreatedOnDemand) {
        Write-Check WARN "$Key sera criado automaticamente no primeiro uso: $Path"
        return
    }

    $status = if ($Optional) { 'WARN' } else { 'FAIL' }
    Write-Check $status "$Key nao encontrado: $Path"
}

$appRoot = [IO.Path]::GetFullPath($AppDirectory)
if (-not (Test-Path -LiteralPath $appRoot -PathType Container)) {
    Write-Check FAIL "Diretorio publicado nao encontrado: $appRoot"
    exit 1
}
Write-Check PASS "Diretorio publicado encontrado: $appRoot"

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $configCandidates = @(
        (Join-Path $appRoot 'TeamOps.UI.dll.config'),
        (Join-Path $appRoot 'TeamOps.UI.exe.config')
    )
    $ConfigPath = $configCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($ConfigPath) -or -not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    Write-Check FAIL 'Arquivo TeamOps.UI.dll.config/exe.config nao encontrado.'
    exit 1
}

[xml]$configuration = Get-Content -LiteralPath $ConfigPath -Raw
$settings = @{}
foreach ($entry in $configuration.configuration.appSettings.add) {
    $settings[[string]$entry.key] = [string]$entry.value
}
Write-Check PASS "Configuracao carregada: $ConfigPath"

$requiredKeys = @(
    'StorageRoot', 'DatabasePath', 'PRDirectory', 'PRTemplate', 'CLDirectory', 'CLTemplate',
    'HikitsuguiAttachmentPath', 'OperatorScheduleDirectory', 'HaidaiExportDirectory',
    'ProductionEventsDirectory', 'ProductionSourceEventsDirectory', 'ProductionSourceDatDirectory'
)
foreach ($key in $requiredKeys) {
    if (-not $settings.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($settings[$key])) {
        Write-Check FAIL "Configuracao obrigatoria ausente: $key"
    }
}
if ($script:Failures -gt 0) { exit 1 }

$storageRoot = Get-FullConfiguredPath -Value $settings.StorageRoot -StorageRoot $appRoot -ApplicationRoot $appRoot -RelativeToApplication
$databasePath = Get-FullConfiguredPath -Value $settings.DatabasePath -StorageRoot $storageRoot -ApplicationRoot $appRoot -RelativeToApplication

if ($storageRoot.StartsWith('\\')) {
    Write-Check PASS "StorageRoot usa UNC: $storageRoot"
} elseif ($AllowLocalStorageRoot) {
    Write-Check WARN "StorageRoot local permitido explicitamente: $storageRoot"
} else {
    Write-Check FAIL "StorageRoot de producao deve usar UNC: $storageRoot"
}

if ($databasePath.StartsWith('\\')) {
    Write-Check FAIL "DatabasePath aponta para rede; SQLite deve permanecer local: $databasePath"
} else {
    $databaseDrive = [IO.Path]::GetPathRoot($databasePath)
    $databaseUsesMappedDrive = $false
    try {
        $databaseUsesMappedDrive = (New-Object IO.DriveInfo($databaseDrive)).DriveType -eq [IO.DriveType]::Network
    } catch {
        $databaseUsesMappedDrive = $false
    }

    if ($databaseUsesMappedDrive) {
        Write-Check FAIL "DatabasePath usa unidade de rede mapeada; SQLite deve permanecer local: $databasePath"
    } else {
        Write-Check PASS "DatabasePath permanece local: $databasePath"
    }
}

Test-ConfiguredLocation -Key 'DatabasePath' -Path $databasePath -Kind File

$directoryDefinitions = @(
    @{ Key = 'PRDirectory'; CreatedOnDemand = $true },
    @{ Key = 'CLDirectory'; CreatedOnDemand = $true },
    @{ Key = 'HikitsuguiAttachmentPath'; CreatedOnDemand = $false },
    @{ Key = 'OperatorScheduleDirectory'; CreatedOnDemand = $false },
    @{ Key = 'HaidaiExportDirectory'; CreatedOnDemand = $false },
    @{ Key = 'ProductionEventsDirectory'; CreatedOnDemand = $false },
    @{ Key = 'ProductionSourceEventsDirectory'; CreatedOnDemand = $false },
    @{ Key = 'ProductionSourceDatDirectory'; CreatedOnDemand = $false }
)
foreach ($definition in $directoryDefinitions) {
    $key = $definition.Key
    try {
        $resolved = Get-FullConfiguredPath -Value $settings[$key] -StorageRoot $storageRoot -ApplicationRoot $appRoot
        Test-ConfiguredLocation -Key $key -Path $resolved -Kind Directory -CreatedOnDemand:$definition.CreatedOnDemand
    } catch {
        Write-Check FAIL "$key invalido: $($_.Exception.Message)"
    }
}

$fileDefinitions = @(
    @{ Key = 'PRTemplate'; Optional = $false; BundledFile = 'ModeloPR.xlsx' },
    @{ Key = 'CLTemplate'; Optional = $false; BundledFile = 'ModeloCL.xlsx' },
    @{ Key = 'ProductionImportBatchPath'; Optional = $true },
    @{ Key = 'Ec2AdministratorFilePath'; Optional = $true }
)
foreach ($definition in $fileDefinitions) {
    $key = $definition.Key
    if (-not $settings.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($settings[$key])) {
        if (-not $definition.Optional) { Write-Check FAIL "Configuracao obrigatoria ausente: $key" }
        continue
    }
    try {
        $resolved = Get-FullConfiguredPath -Value $settings[$key] -StorageRoot $storageRoot -ApplicationRoot $appRoot
        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            Test-ConfiguredLocation -Key $key -Path $resolved -Kind File -Optional:$definition.Optional
        } elseif ($definition.ContainsKey('BundledFile')) {
            $bundledPath = Join-Path $appRoot "Modelos\$($definition.BundledFile)"
            if (Test-Path -LiteralPath $bundledPath -PathType Leaf) {
                Write-Check PASS "$key sera provisionado pelo pacote: $bundledPath"
            } else {
                Write-Check FAIL "$key nao encontrado e modelo empacotado ausente: $bundledPath"
            }
        } else {
            Test-ConfiguredLocation -Key $key -Path $resolved -Kind File -Optional:$definition.Optional
        }
    } catch {
        Write-Check FAIL "$key invalido: $($_.Exception.Message)"
    }
}

$uiModules = @('dashboard', 'access-control', 'admin', 'hikitsugui-create', 'production-monitor')
foreach ($module in $uiModules) {
    $modulePath = Join-Path $appRoot "ui\$module\index.html"
    Test-ConfiguredLocation -Key "WebView:$module" -Path $modulePath -Kind File
}

if ($TestWrite) {
    $probeTargets = @($storageRoot, [IO.Path]::GetDirectoryName($databasePath))
    foreach ($target in $probeTargets | Select-Object -Unique) {
        try {
            $probe = Join-Path $target ('.teamops-write-probe-' + [Guid]::NewGuid().ToString('N') + '.tmp')
            [IO.File]::WriteAllText($probe, 'TeamOps deployment write probe')
            Remove-Item -LiteralPath $probe -Force
            Write-Check PASS "Escrita confirmada: $target"
        } catch {
            Write-Check FAIL "Sem permissao de escrita em $target`: $($_.Exception.Message)"
        }
    }
} else {
    Write-Check WARN 'Teste de escrita nao executado. Use -TestWrite durante a homologacao controlada.'
}

Write-Host ""
Write-Host "Resultado: $script:Failures falha(s), $script:Warnings aviso(s)."
if ($script:Failures -gt 0) { exit 1 }
exit 0
