param(
    [Parameter(Mandatory=$true)][string]$PortableSource,
    [Parameter(Mandatory=$true)][string]$Destination,
    [Parameter(Mandatory=$true)][string]$DuckovDirectory
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($PortableSource)
$stage = [IO.Path]::GetFullPath($Destination)
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (Test-Path -LiteralPath $stage) { throw 'Destination must be a new directory.' }
$minecraftSource = Join-Path $source 'minecraft'
if (Test-Path -LiteralPath (Join-Path $minecraftSource 'saves')) { throw 'Use a pristine portable source without Minecraft saves.' }
& dotnet build (Join-Path $repo 'duckov\DuckovCraft.csproj') -c Release "-p:DuckovDir=$DuckovDirectory" --nologo
if ($LASTEXITCODE -ne 0) { throw 'DuckovCraft build failed.' }
$bundle = Join-Path $stage 'DuckovCraft'
$payload = Join-Path $bundle 'payload\Duckov_Data\Mods\DuckovCraft'
New-Item -ItemType Directory -Force -Path $payload, (Join-Path $bundle 'launcher') | Out-Null
foreach ($directory in @('runtime','minecraft')) { Copy-Item -LiteralPath (Join-Path $source $directory) -Destination (Join-Path $bundle $directory) -Recurse }
Copy-Item -LiteralPath (Join-Path $source 'client.json'), (Join-Path $source 'LICENSE-SkyCraft') -Destination $bundle
Copy-Item -LiteralPath (Join-Path $repo 'duckov\bin\Release\netstandard2.1\DuckovCraft.dll'), (Join-Path $repo 'duckov\info.ini'), (Join-Path $repo 'duckov\assets\duckovcraft-assets') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-DuckovCraft.ps1') -Destination (Join-Path $bundle 'launcher')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-DuckovCraft.bat') -Destination (Join-Path $stage '启动鸭科夫MC.bat')
Copy-Item -LiteralPath (Join-Path $repo 'duckov\README.md') -Destination (Join-Path $stage 'DuckovCraft-使用说明.md')
$settings = @{ playerName = 'Steve'; maxMemoryMiB = 4096 }
[IO.File]::WriteAllText((Join-Path $bundle 'launcher.json'), ($settings | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
    @{ path = $_.FullName.Substring($stage.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[IO.File]::WriteAllText((Join-Path $bundle 'files.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Host "Prepared $stage ($($manifest.Count) files)."
