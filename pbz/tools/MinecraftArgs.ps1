function New-PBZCraftMinecraftArgs([string]$Root,[string]$Bundle) {
    $metadata=Get-Content -LiteralPath (Join-Path $Bundle 'client.json') -Raw | ConvertFrom-Json
    $gameArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $Bundle 'minecraft/client.jar'))
    try {
        $reader=[IO.StreamReader]::new($gameArchive.GetEntry('version.json').Open())
        try {$gameVersion=$reader.ReadToEnd()|ConvertFrom-Json} finally {$reader.Dispose()}
    } finally {$gameArchive.Dispose()}
    if($gameVersion.id -ne '26.3'){throw 'This preview requires the existing Minecraft 26.3 client.'}
    $client=Join-Path $Root 'Client'
    foreach($part in @('mods','natives/java','natives/lwjgl')){[IO.Directory]::CreateDirectory((Join-Path $client $part))|Out-Null}
    Copy-Item -Path (Join-Path $Root 'minecraft-mods/*.jar') -Destination (Join-Path $client 'mods') -Force
    $libraries=@($metadata.libraryPaths | ForEach-Object {Join-Path (Join-Path $Bundle 'minecraft') $_})
    foreach($library in $libraries | Where-Object {$_ -like '*natives-windows.jar'}) {
        $archive=[IO.Compression.ZipFile]::OpenRead($library)
        try {
            foreach($entry in $archive.Entries | Where-Object {$_.Name -like '*.dll'}) {
                $destination=Join-Path $client ('natives/java/'+$entry.Name)
                if(!(Test-Path -LiteralPath $destination)){[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination,$false)}
            }
        } finally {$archive.Dispose()}
    }
    $classpath=(@($libraries)+(Join-Path $Bundle 'minecraft/client.jar') | ForEach-Object {[IO.Path]::GetRelativePath($client,$_ )}) -join ';'
    $assets=[IO.Path]::GetRelativePath($client,(Join-Path $Bundle 'minecraft/assets'))
    $arguments=@('-XX:+UseCompactObjectHeaders','-XX:StackShadowPages=32','-XX:ErrorFile=../logs/jvm-%p.log','-Xms512M','-Xmx3072M','-XX:+UseZGC','--enable-native-access=ALL-UNNAMED','--add-exports=java.base/jdk.internal.misc=ALL-UNNAMED','-Djava.library.path=natives/java','-Dorg.lwjgl.system.SharedLibraryExtractPath=natives/lwjgl','-Dskycraft.host=sulfur','-Dskycraft.link=Local\PBZCraft_v1','-Dskycraft.startHidden=true','-Dskycraft.quitWithSkyrim=true','-Dskycraft.discordAppId=0','-cp',$classpath,$metadata.mainClass,'--username','Steve','--uuid','5627dd98-e6be-3c21-b8a8-e92344183641','--accessToken','0','--version','26.3-PBZCraft','--gameDir','.','--assetsDir',$assets,'--assetIndex',[string]$metadata.assetIndex)
    $arguments+=@('--versionType','release','--width','1280','--height','720')
    $lines=@($arguments | ForEach-Object {'"'+$_.Replace('\','\\').Replace('"','\"')+'"'})
    $argsFile=Join-Path $Root 'minecraft.args'
    [IO.File]::WriteAllLines($argsFile,$lines,[Text.UTF8Encoding]::new($false))
    return @{Java=(Join-Path $Bundle 'runtime/bin/java.exe');Arguments=$argsFile;Directory=$client}
}
