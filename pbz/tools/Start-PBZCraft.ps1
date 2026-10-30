param([string]$Root=(Split-Path $PSScriptRoot -Parent),[switch]$Demo,[ValidateScript({$_ -eq 0 -or ($_ -ge 90 -and $_ -le 86400)})][int]$Seconds=0,[DateTimeOffset]$Deadline=[DateTimeOffset]::MaxValue)
$runLock=[Threading.Mutex]::new($false,'Local\PBZCraft.PreviewLauncher')
$lockOwned=$false
try {
try {$lockOwned=$runLock.WaitOne(0)} catch [Threading.AbandonedMutexException] {$lockOwned=$true}
if(!$lockOwned){throw 'A previous preview is still running or cleaning up.'}
$ErrorActionPreference='Stop'
if([DateTimeOffset]::Now -ge $Deadline){throw 'The requested test deadline has passed.'}
if($Demo -and $Seconds -eq 0){$Seconds=300}
$Root=[IO.Path]::GetFullPath($Root)
$gameRoot=Split-Path $Root -Parent
$nativeRoot=Join-Path $gameRoot 'platform/steam'
$nativeExe=Join-Path $nativeRoot 'PBZ.exe'
$proxy=Join-Path $nativeRoot 'dwmapi.dll'
$loader=Join-Path $Root 'loader'
$nativeLoader=Join-Path $nativeRoot 'ue4ss'
$config=Get-Content -LiteralPath (Join-Path $Root 'config.json') -Raw | ConvertFrom-Json
if(Get-Process -Name PBZ,'PBZCraft.Trial' -ErrorAction SilentlyContinue){throw 'Close the existing PBZ/preview before starting this preview.'}
if((Get-FileHash -LiteralPath $nativeExe -Algorithm SHA256).Hash -ne $config.GameSHA256){throw 'The game build differs from the tested PBZ build. No files were changed.'}
foreach($name in @('UE4SS.dll','UE4SS-settings.ini','MemberVariableLayout.ini','UE4SS_Signatures/GNatives.lua','UE4SS_Signatures/GUObjectArray.lua','Mods/mods.txt')) {
    $destination=Join-Path $nativeLoader $name
    if((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath (Join-Path $loader ('ue4ss/'+$name))).Hash){throw "Existing UE4SS file differs: $name"}
}
if(Test-Path -LiteralPath $proxy){if((Get-FileHash -LiteralPath $proxy).Hash -ne (Get-FileHash -LiteralPath (Join-Path $loader 'dwmapi.dll')).Hash){throw 'A different existing proxy loader was found.'}}
. (Join-Path $PSScriptRoot 'MinecraftArgs.ps1')
$bundle=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted([string]$config.MinecraftBundle)){[string]$config.MinecraftBundle}else{Join-Path $Root ([string]$config.MinecraftBundle)}))
[IO.Directory]::CreateDirectory((Join-Path $Root 'logs'))|Out-Null
$mc=New-PBZCraftMinecraftArgs $Root $bundle
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$saveRoot=Join-Path $gameRoot 'ProjectPBZ/Saved/SaveGames'
$saveBackup=Join-Path $Root ('backups/SaveGames-'+$stamp)
if(Test-Path -LiteralPath $saveRoot){Copy-Item -LiteralPath $saveRoot -Destination $saveBackup -Recurse}
Copy-Item -LiteralPath (Join-Path $loader 'ue4ss') -Destination $nativeRoot -Recurse -Force
$proxyWasPresent=Test-Path -LiteralPath $proxy
$game=$null;$hostProcess=$null;$minecraft=$null;$forced=$false;$saveRestored=$false
function Request-NativeQuit {
    try {
        $map=[IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\PBZCraft.Native_v1')
        $view=$map.CreateViewAccessor(0,4096)
        try {$seq=$view.ReadInt32(2048);$view.Write(2048,[int]($seq+1));$view.Write(2052,[int]48);$view.Write(2056,[ulong][Environment]::TickCount64);$view.WriteArray[double](2064,[double[]]@(-1,0,0,90,0,0),0,6);$view.Write(2048,[int]($seq+2))}
        finally {$view.Dispose();$map.Dispose()}
    } catch [IO.FileNotFoundException] {}
}
try {
    if(!$proxyWasPresent){Copy-Item -LiteralPath (Join-Path $loader 'dwmapi.dll') -Destination $proxy}
    $env:PBZCRAFT_DEADLINE=$Deadline.ToString('o')
    $env:PBZCRAFT_EXCHANGE=Join-Path $Root ('exchange/'+$stamp)
    $arguments=@(('"'+$Root+'"'),$(if($Seconds -gt 0){$Seconds-10}else{0}),'native','overlay','preview','nocapture')
    if(!$Demo){$arguments+='interactive'}
    $hostProcess=Start-Process -FilePath (Join-Path $Root 'host/PBZCraft.Trial.exe') -ArgumentList $arguments -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Root ('logs/'+$stamp+'-host.log')) -RedirectStandardError (Join-Path $Root ('logs/'+$stamp+'-host-error.log')) -PassThru
    $minecraft=Start-Process -FilePath $mc.Java -ArgumentList ('@"'+$mc.Arguments+'"') -WorkingDirectory $mc.Directory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Root ('logs/'+$stamp+'-minecraft.log')) -RedirectStandardError (Join-Path $Root ('logs/'+$stamp+'-minecraft-error.log')) -PassThru
    $info=[Diagnostics.ProcessStartInfo]::new()
    $info.FileName=$nativeExe;$info.WorkingDirectory=$nativeRoot;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.Environment['SteamAppId']='4115450';$info.Environment['SteamGameId']='4115450'
    $info.Environment['PBZCRAFT_PROBE_SECONDS']=[string]$(if($Seconds -gt 0){$Seconds+5}else{0})
    $info.Environment['PBZCRAFT_MOD_ROOT']=Join-Path $nativeLoader 'Mods/PBZProbe'
    $info.Environment['PBZCRAFT_EXCHANGE']=$env:PBZCRAFT_EXCHANGE
    $info.Environment['PBZCRAFT_TEXTURE_BRIGHTNESS']=[string]$(if($null -ne $config.TextureBrightness){[Math]::Clamp([double]$config.TextureBrightness,0.01,1)}else{0.15})
    if($Deadline -ne [DateTimeOffset]::MaxValue){$info.Environment['PBZCRAFT_DEADLINE_UNIX']=[string]$Deadline.ToUnixTimeSeconds()}
    foreach($arg in @('ProjectPBZ',('-basedir='+(Join-Path $gameRoot 'ProjectPBZ/Binaries/Win64')),'-pc-platform=Steam','-windowed')){$info.ArgumentList.Add($arg)}
    if($Demo){$info.ArgumentList.Add('-unattended')}
    $game=[Diagnostics.Process]::Start($info)
    @{GamePID=$game.Id;HostPID=$hostProcess.Id;MinecraftPID=$minecraft.Id;Started=(Get-Date).ToString('o');Demo=[bool]$Demo;Seconds=$Seconds;MinecraftBundle=$bundle}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Root 'current-run.json') -Encoding utf8
    Write-Host 'PBZCraft preview started. Native world stays running; MC owns player movement. F9 ends the preview.'
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while(!$game.HasExited -and ($Seconds -eq 0 -or $timer.Elapsed.TotalSeconds -lt $Seconds) -and [DateTimeOffset]::Now -lt $Deadline) {
        if($minecraft.HasExited){throw ('Minecraft 启动或运行失败，请查看 PBZCraft/logs/'+$stamp+'-minecraft-error.log')}
        if($hostProcess.HasExited){if($hostProcess.ExitCode -ne 0){throw ('桥接程序运行失败，请查看 PBZCraft/logs/'+$stamp+'-host-error.log')};Request-NativeQuit;break}
        $game.WaitForExit(500)|Out-Null
    }
} finally {
    $expired=[DateTimeOffset]::Now -ge $Deadline
    Remove-Item Env:PBZCRAFT_DEADLINE -ErrorAction SilentlyContinue
    Remove-Item Env:PBZCRAFT_EXCHANGE -ErrorAction SilentlyContinue
    if($hostProcess -and !$hostProcess.HasExited){$stopRoot=Join-Path $Root ('exchange/'+$stamp);[IO.Directory]::CreateDirectory($stopRoot)|Out-Null;[IO.File]::WriteAllText((Join-Path $stopRoot 'stop.request'),'stop')}
    if($game -and !$game.HasExited){if(!$expired){Request-NativeQuit;$game.WaitForExit(8000)|Out-Null};if(!$game.HasExited){$forced=$true;$game.Kill();$game.WaitForExit(3000)|Out-Null}}
    foreach($owned in @($hostProcess,$minecraft)){if($owned -and !$owned.HasExited){if(!$expired){$owned.WaitForExit(12000)|Out-Null};if(!$owned.HasExited){$forced=$true;$owned.Kill();$owned.WaitForExit(3000)|Out-Null}}}
    if(!$proxyWasPresent -and (Test-Path -LiteralPath $proxy) -and !(Get-Process PBZ -ErrorAction SilentlyContinue | Where-Object {!$_.HasExited})){Remove-Item -LiteralPath $proxy}
    if((Test-Path -LiteralPath $saveBackup) -and !(Get-Process PBZ -ErrorAction SilentlyContinue | Where-Object {!$_.HasExited})) {
        $resolvedSave=[IO.Path]::GetFullPath($saveRoot)
        foreach($file in Get-ChildItem -LiteralPath $saveRoot -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($resolvedSave,$file.FullName)
            if(!(Test-Path -LiteralPath (Join-Path $saveBackup $relative))) {
                if(!$file.FullName.StartsWith($resolvedSave+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Save cleanup path is outside the named SaveGames directory.'}
                Remove-Item -LiteralPath $file.FullName
            }
        }
        Copy-Item -Path (Join-Path $saveBackup '*') -Destination $saveRoot -Recurse -Force
        $before=@(Get-ChildItem -LiteralPath $saveBackup -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($saveBackup,$_.FullName)+' '+(Get-FileHash -LiteralPath $_.FullName).Hash} | Sort-Object)
        $after=@(Get-ChildItem -LiteralPath $saveRoot -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($saveRoot,$_.FullName)+' '+(Get-FileHash -LiteralPath $_.FullName).Hash} | Sort-Object)
        if(@(Compare-Object $before $after).Count -ne 0){throw 'Save restoration hash verification failed; backup retained.'}
        $saveRestored=$true
    }
    @{Ended=(Get-Date).ToString('o');ForcedCleanup=$forced;GameExit=if($game -and $game.HasExited){$game.ExitCode}else{$null};HostExit=if($hostProcess -and $hostProcess.HasExited){$hostProcess.ExitCode}else{$null};MinecraftExit=if($minecraft -and $minecraft.HasExited){$minecraft.ExitCode}else{$null};SaveRestored=$saveRestored;ProxyRestored=($proxyWasPresent -eq (Test-Path -LiteralPath $proxy))}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Root ('logs/'+$stamp+'-launch-result.json')) -Encoding utf8
    Write-Host ('Preview ended; save restoration verified: '+$saveRestored)
}
} finally {if($lockOwned){$runLock.ReleaseMutex()};$runLock.Dispose()}
