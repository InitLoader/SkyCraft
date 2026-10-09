param([switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'

function JavaArgument([string]$Value) {
    return '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

try {
    $bundle = Split-Path $PSScriptRoot -Parent
    $gameRoot = Split-Path $bundle -Parent
    if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'Duckov.exe'))) {
        throw '请把启动文件和 DuckovCraft 文件夹一起放到鸭科夫根目录（Duckov.exe 所在目录）。'
    }
    $client = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $bundle 'client.json') | ConvertFrom-Json
    $settings = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $bundle 'launcher.json') | ConvertFrom-Json
    if ($settings.playerName -notmatch '^[A-Za-z0-9_]{1,16}$') { throw '玩家名应为 1 至 16 位英文字母、数字或下划线。' }
    if ($settings.maxMemoryMiB -lt 1024 -or $settings.maxMemoryMiB -gt 32768) { throw '内存设置应在 1024 至 32768 MiB 之间。' }
    $java = Join-Path $bundle 'runtime\bin\java.exe'
    $minecraft = Join-Path $bundle 'minecraft'
    $libraries = @($client.libraryPaths | ForEach-Object { Join-Path $minecraft $_ })
    foreach ($file in @($java, (Join-Path $minecraft 'client.jar'), (Join-Path $minecraft "assets\indexes\$($client.assetIndex).json")) + $libraries) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "整合包文件缺失：$file" }
    }
    if ($ValidateOnly) {
        $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $bundle 'files.json') | ConvertFrom-Json
        foreach ($entry in $manifest) {
            $file = Join-Path $gameRoot $entry.path
            if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256) {
                throw "整合包校验失败：$($entry.path)"
            }
        }
        Write-Host "整合包校验通过：$($manifest.Count) 个文件，$($libraries.Count) 个运行库。"
        exit 0
    }

    try {
        $mapping = [IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\DuckovCraft_v1', [IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
        try {
            $view = $mapping.CreateViewAccessor(0, 32, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
            try { $clientId = $view.ReadUInt32(12) } finally { $view.Dispose() }
        } finally { $mapping.Dispose() }
        $existing = if ($clientId -gt 0) { Get-Process -Id $clientId -ErrorAction SilentlyContinue } else { $null }
        if ($existing) {
            if ($existing.Path -ne $java) { throw '检测到另一个 Minecraft 桥接客户端，请先正常退出旧客户端再启动整合包。' }
            if (-not (Get-Process -Name Duckov -ErrorAction SilentlyContinue)) { Start-Process 'steam://rungameid/3167020' -WindowStyle Hidden }
            Write-Host '整合包客户端已经运行，本次不会重复启动。'
            exit 0
        }
    } catch [IO.FileNotFoundException] { }

    $payload = Join-Path $bundle 'payload'
    foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File) {
        $relative = $file.FullName.Substring($payload.Length + 1)
        $destination = Join-Path $gameRoot $relative
        $bridgeFile = $relative.StartsWith('Duckov_Data\Mods\DuckovCraft\', [StringComparison]::OrdinalIgnoreCase)
        if (Test-Path -LiteralPath $destination) {
            if (-not $bridgeFile -or (Get-FileHash -LiteralPath $destination).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) { continue }
            if (Get-Process -Name Duckov -ErrorAction SilentlyContinue) { throw '请先正常退出鸭科夫，启动器需要更新桥接插件。' }
            $backup = Join-Path $bundle ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '\' + $relative)
            New-Item -ItemType Directory -Force -Path (Split-Path $backup -Parent) | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backup
        }
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }

    $logs = Join-Path $bundle 'logs'
    $natives = Join-Path $minecraft 'natives'
    New-Item -ItemType Directory -Force -Path $logs, $natives | Out-Null
    $md5 = [Security.Cryptography.MD5]::Create()
    try { $uuidBytes = $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('OfflinePlayer:' + $settings.playerName)) } finally { $md5.Dispose() }
    $uuidBytes[6] = ($uuidBytes[6] -band 15) -bor 48
    $uuidBytes[8] = ($uuidBytes[8] -band 63) -bor 128
    $uuid = [BitConverter]::ToString($uuidBytes).Replace('-', '').ToLowerInvariant()
    $arguments = @(
        '-Xms512M', "-Xmx$($settings.maxMemoryMiB)M", '-XX:+UseZGC', '-XX:+UseCompactObjectHeaders',
        '-XX:StackShadowPages=32', '--enable-native-access=ALL-UNNAMED', '--add-exports', 'java.base/jdk.internal.misc=ALL-UNNAMED',
        '-Djava.library.path=natives\java', '-Djna.tmpdir=natives\jna', '-Dorg.lwjgl.system.SharedLibraryExtractPath=natives\lwjgl',
        '-Dio.netty.native.workdir=natives\netty', '-Dminecraft.launcher.brand=DuckovCraft', '-Dminecraft.launcher.version=0.1.3',
        '-Dskycraft.host=sulfur', '-Dskycraft.link=Local\DuckovCraft_v1', '-Dskycraft.startHidden=true', '-Dskycraft.quitWithSkyrim=true', '-Dskycraft.discordAppId=0',
        '-cp', (($client.libraryPaths + 'client.jar') -join ';'), $client.mainClass,
        '--username', $settings.playerName, '--uuid', $uuid, '--accessToken', '0', '--version', $client.version,
        '--gameDir', '.', '--assetsDir', 'assets', '--assetIndex', $client.assetIndex,
        '--versionType', 'release', '--width', '1280', '--height', '720'
    )
    $argumentFile = Join-Path $logs 'minecraft.args'
    [IO.File]::WriteAllLines($argumentFile, @($arguments | ForEach-Object { JavaArgument ([string]$_) }), [Text.UTF8Encoding]::new($false))
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $process = Start-Process -FilePath $java -ArgumentList ('"@' + $argumentFile + '"') -WorkingDirectory $minecraft -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logs "$stamp-minecraft.log") -RedirectStandardError (Join-Path $logs "$stamp-minecraft-error.log")
    Write-Host "Minecraft 已启动（PID $($process.Id)），正在通过 Steam 启动鸭科夫……"
    if (-not (Get-Process -Name Duckov -ErrorAction SilentlyContinue)) {
        Start-Process 'steam://rungameid/3167020' -WindowStyle Hidden
    }
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { throw "Minecraft 启动失败，请查看 $logs 下的错误日志。" }
        try {
            $mapping = [IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\DuckovCraft_v1', [IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
            try {
                $view = $mapping.CreateViewAccessor(0, 0x308, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
                try { $linked = $view.ReadUInt32(12) -eq $process.Id -and ($view.ReadUInt32(0x204) -band 1) -ne 0 } finally { $view.Dispose() }
            } finally { $mapping.Dispose() }
        } catch [IO.FileNotFoundException] { $linked = $false }
    } while (-not $linked -and (Get-Date) -lt $deadline)
    if ($linked) { Write-Host '两端已连接。首次请在主菜单的 Mods 中启用 DuckovCraft，然后进入鸭科夫存档；正常退出鸭科夫后，Minecraft 会自动保存并关闭。' }
    else { Write-Host "两边已启动，仍在等待连接。Steam 如需登录请完成登录；日志位于 $logs。" }
    exit 0
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
