param(
    [Parameter(Mandatory=$true)][string]$MinecraftRoot,
    [Parameter(Mandatory=$true)][string]$ProfileDirectory,
    [Parameter(Mandatory=$true)][string]$JavaHome,
    [Parameter(Mandatory=$true)][string]$SulfurDirectory,
    [Parameter(Mandatory=$true)][string]$BridgeJar,
    [Parameter(Mandatory=$true)][string]$PluginDll,
    [Parameter(Mandatory=$true)][string]$AssetBundle,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force)) {
    throw 'OutputDirectory must be empty. Existing runtime worlds must not be overwritten.'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$bundle = Join-Path $OutputDirectory 'SulfurCraft'
$minecraft = Join-Path $bundle 'minecraft'
New-Item -ItemType Directory -Force -Path $minecraft | Out-Null

function CopyChecked([string]$Source, [string]$Destination, [string]$Sha1 = '') {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing source file: $Source" }
    if ($Sha1 -and (Get-FileHash -LiteralPath $Source -Algorithm SHA1).Hash -ne $Sha1) { throw "SHA1 mismatch: $Source" }
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination -Parent) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination
}

function WindowsLibrary($Library) {
    if ($Library.name -match 'natives-.*(arm64|aarch64|x86)$') { return $false }
    if (-not $Library.rules) { return $true }
    $allowed = $false
    foreach ($rule in $Library.rules) {
        if ($rule.os.name -and $rule.os.name -ne 'windows') { continue }
        if ($rule.os.arch -and 'amd64' -notmatch $rule.os.arch) { continue }
        if ($rule.os.version -and [Environment]::OSVersion.Version.ToString() -notmatch $rule.os.version) { continue }
        if ($rule.features) { continue }
        $allowed = $rule.action -eq 'allow'
    }
    return $allowed
}

$versionFile = Get-Item -LiteralPath (Join-Path $ProfileDirectory ((Split-Path $ProfileDirectory -Leaf) + '.json'))
$version = Get-Content -Raw -LiteralPath $versionFile.FullName | ConvertFrom-Json
if ($version.inheritsFrom -or $version.javaVersion.majorVersion -ne 25) { throw 'Expected the merged Java 25 Minecraft profile.' }
CopyChecked (Join-Path $ProfileDirectory ($versionFile.BaseName + '.jar')) (Join-Path $minecraft 'client.jar') $version.downloads.client.sha1
$paths = @()
foreach ($library in $version.libraries) {
    if (-not (WindowsLibrary $library)) { continue }
    $path = $library.downloads.artifact.path
    if (-not $path) {
        $parts = $library.name.Split(':')
        $suffix = if ($parts.Count -gt 3) { '-' + $parts[3] } else { '' }
        $path = $parts[0].Replace('.', '/') + '/' + $parts[1] + '/' + $parts[2] + '/' + $parts[1] + '-' + $parts[2] + $suffix + '.jar'
    }
    if ($paths -contains ('libraries/' + $path)) { continue }
    CopyChecked (Join-Path $MinecraftRoot ('libraries/' + $path)) (Join-Path $minecraft ('libraries/' + $path)) $library.downloads.artifact.sha1
    $paths += 'libraries/' + $path
}
Write-Host "Copied $($paths.Count) Windows libraries."
$indexPath = Join-Path $MinecraftRoot ("assets/indexes/$($version.assetIndex.id).json")
CopyChecked $indexPath (Join-Path $minecraft ("assets/indexes/$($version.assetIndex.id).json")) $version.assetIndex.sha1
$index = Get-Content -Raw -LiteralPath $indexPath | ConvertFrom-Json
$hashes = @($index.objects.PSObject.Properties | ForEach-Object { $_.Value.hash } | Sort-Object -Unique)
$count = 0
foreach ($hash in $hashes) {
    $relative = 'assets/objects/' + $hash.Substring(0, 2) + '/' + $hash
    CopyChecked (Join-Path $MinecraftRoot $relative) (Join-Path $minecraft $relative) $hash
    $count++
    if ($count % 1000 -eq 0) { Write-Host "Assets $count/$($hashes.Count)" }
}
New-Item -ItemType Directory -Force -Path (Join-Path $minecraft 'mods'), (Join-Path $minecraft 'config') | Out-Null
CopyChecked $BridgeJar (Join-Path $minecraft 'mods/sulfurcraft.jar')
$api = @(Get-ChildItem -LiteralPath (Join-Path $ProfileDirectory 'mods') -Filter 'fabric-api-*.jar' -File)
if ($api.Count -ne 1) { throw 'Expected one Fabric API jar in the source profile.' }
CopyChecked $api[0].FullName (Join-Path $minecraft ('mods/' + $api[0].Name))
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $minecraft 'config/skycraft.properties'), "join=`n", $utf8)
$runtime = Join-Path $bundle 'runtime'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
foreach ($directory in 'bin', 'conf', 'lib', 'legal') { Copy-Item -LiteralPath (Join-Path $JavaHome $directory) -Destination $runtime -Recurse }
# Source archives are not needed to run Java.
$sourceArchive = Join-Path $runtime 'lib/src.zip'
if (Test-Path -LiteralPath $sourceArchive) { Remove-Item -LiteralPath $sourceArchive }
foreach ($file in 'NOTICE', 'release') { CopyChecked (Join-Path $JavaHome $file) (Join-Path $runtime $file) }
$payload = Join-Path $bundle 'payload'
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $SulfurDirectory 'BepInEx/core') -Filter '*.dll' -File) {
    CopyChecked $file.FullName (Join-Path $payload ('BepInEx/core/' + $file.Name))
}
foreach ($file in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') { CopyChecked (Join-Path $SulfurDirectory $file) (Join-Path $payload $file) }
CopyChecked $PluginDll (Join-Path $payload 'BepInEx/plugins/SulfurCraft/SulfurCraft.dll')
CopyChecked $AssetBundle (Join-Path $payload 'BepInEx/plugins/SulfurCraft/sulfurcraft-assets')
$launcher = Join-Path $bundle 'launcher'
New-Item -ItemType Directory -Force -Path $launcher | Out-Null
[IO.File]::WriteAllText((Join-Path $launcher 'Start-SulfurCraft.ps1'), [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Start-SulfurCraft.ps1')), [Text.UTF8Encoding]::new($true))
CopyChecked (Join-Path $PSScriptRoot 'Start-SulfurCraft.bat') (Join-Path $OutputDirectory '启动火湖MC.bat')
$client = @{ version = '26.3-SulfurCraft'; mainClass = $version.mainClass; assetIndex = $version.assetIndex.id; libraryPaths = $paths }
[IO.File]::WriteAllText((Join-Path $bundle 'client.json'), ($client | ConvertTo-Json -Depth 5), $utf8)
[IO.File]::WriteAllText((Join-Path $bundle 'launcher.json'), (@{ playerName = 'Steve'; maxMemoryMiB = 4096 } | ConvertTo-Json), $utf8)
CopyChecked (Join-Path $PSScriptRoot 'README.md') (Join-Path $OutputDirectory '整合包说明.md')
CopyChecked (Join-Path $PSScriptRoot '../../LICENSE') (Join-Path $bundle 'LICENSE-SkyCraft')
$manifest = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | ForEach-Object {
    @{ path = $_.FullName.Substring($OutputDirectory.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[IO.File]::WriteAllText((Join-Path $bundle 'files.json'), ($manifest | ConvertTo-Json -Depth 4), $utf8)
Write-Host "Portable bundle ready: $OutputDirectory ($($manifest.Count) files). No worlds or account credentials copied."
