param([Parameter(Mandatory)][string]$Root)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../tools/MinecraftArgs.ps1')
$Root=[IO.Path]::GetFullPath($Root)
$launch=New-PBZCraftMinecraftArgs $Root $Root
$lines=[IO.File]::ReadAllLines($launch.Arguments)
if(($lines -join '').ToCharArray() | Where-Object {[int]$_ -gt 127}){throw 'A full-bundle argument file contains locale-dependent installation paths.'}
$classpath=$lines[[Array]::IndexOf($lines,'"-cp"')+1].Trim('"').Replace('\\','\')
foreach($entry in $classpath.Split(';')) {
    if([IO.Path]::IsPathRooted($entry)){throw ('Classpath must be relative: '+$entry)}
    if(!(Test-Path -LiteralPath (Join-Path $launch.Directory $entry))){throw ('Missing classpath file: '+$entry)}
}
if($lines[[Array]::IndexOf($lines,'"--gameDir"')+1] -ne '"."'){throw 'Game directory is not relative to the owned working directory.'}
'PASS: portable Minecraft arguments stay ASCII, use relative paths, and resolve all classpath entries.'
