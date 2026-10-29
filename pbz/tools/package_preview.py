from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import argparse
import hashlib
import json
import shutil

parser=argparse.ArgumentParser()
parser.add_argument("--stage",type=Path,required=True)
parser.add_argument("--zip",type=Path,required=True)
parser.add_argument("--minecraft",type=Path,required=True)
parser.add_argument("--ue4ss",type=Path,required=True)
parser.add_argument("--proxy",type=Path,required=True)
args=parser.parse_args()
trial=Path(__file__).resolve().parent.parent
stage=args.stage.resolve()
stage.mkdir(parents=True,exist_ok=False)
payload=stage/"PBZCraft"
native=args.ue4ss

def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,target)

for file in (trial/"host/bin/Release/net8.0-windows").iterdir():
    if file.is_file() and file.suffix.lower()!=".pdb":copy(file,payload/"host"/file.name)
for name in ["UE4SS.dll","LICENSE"]:
    copy(native/name,payload/"loader/ue4ss"/name)
for file in (trial/"loader-config").rglob("*"):
    if file.is_file():copy(file,payload/"loader/ue4ss"/file.relative_to(trial/"loader-config"))
copy(args.proxy,payload/"loader/dwmapi.dll")
scripts=["main","PBZResume","PBZIpcProbe","PBZGroundProbe","PBZRayProbe","PBZViewBridge","PBZMeshView","PBZCameraEffects","PBZCollision","PBZOrigin","PBZInput","PBZObstacleProbe","PBZTexture"]
for name in scripts:copy(trial/"mod/Scripts"/(name+".lua"),payload/"loader/ue4ss/Mods/PBZProbe/Scripts"/(name+".lua"))
copy(trial/"native/PBZNative.dll",payload/"loader/ue4ss/Mods/PBZProbe/PBZNative.dll")
copy(trial/"build/PBZCraft.jar",payload/"minecraft-mods/PBZCraft.jar")
api=next((args.minecraft/"minecraft/mods").glob("fabric-api-0.162.0+26.3.jar"))
copy(api,payload/"minecraft-mods"/api.name)
with ZipFile(api) as archive:
    license_file=payload/"licenses/Fabric-API-Apache-2.0.txt"
    license_file.parent.mkdir(parents=True,exist_ok=True)
    license_file.write_bytes(archive.read("LICENSE-fabric-api"))
for name in ["Start-PBZCraft.ps1","MinecraftArgs.ps1"]:copy(trial/"tools"/name,payload/"tools"/name)
config={"MinecraftBundle":str(args.minecraft.resolve()),"GameSHA256":json.loads((trial/"profile.json").read_text(encoding="utf-8"))["GameSHA256"],"TextureBrightness":0.15}
(payload/"config.json").write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding="utf-8")
copy(trial/"使用说明.txt",stage/"PBZCraft使用说明.txt")
copy(trial.parent/"LICENSE",payload/"licenses/PBZCraft-MIT.txt")
copy(native/"LICENSE",payload/"licenses/UE4SS-MIT.txt")
for filename,mode in [("启动PBZCraft预览.cmd",""),("PBZCraft自动演示.cmd"," -Demo")]:
    content='@echo off\r\nchcp 65001 >nul\r\nwhere pwsh.exe >nul 2>&1\r\nif errorlevel 1 (echo PowerShell 7 is required.& pause& exit /b 1)\r\npwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0PBZCraft\\tools\\Start-PBZCraft.ps1"'+mode+' %*\r\nif errorlevel 1 pause\r\n'
    (stage/filename).write_bytes(content.encode("utf-8"))
hashes=[]
for file in sorted(stage.rglob("*")):
    if file.is_file():hashes.append(hashlib.sha256(file.read_bytes()).hexdigest()+"  "+file.relative_to(stage).as_posix())
(stage/"PBZCraft.sha256").write_text("\n".join(hashes)+"\n",encoding="utf-8")
args.zip.parent.mkdir(parents=True,exist_ok=True)
with ZipFile(args.zip,"x",ZIP_DEFLATED,compresslevel=6) as archive:
    for file in sorted(stage.rglob("*")):
        if file.is_file():archive.write(file,file.relative_to(stage).as_posix())
with ZipFile(args.zip) as archive:
    assert archive.testzip() is None
    assert not any(part.lower() in {"saves","logs","backups","cxxheaderdump"} for name in archive.namelist() for part in Path(name).parts)
    assert not any(name.lower().endswith((".pdb",".dmp")) or Path(name).name=="client.jar" for name in archive.namelist())
print(json.dumps({"stage":str(stage),"zip":str(args.zip.resolve()),"files":len(hashes)+1,"bytes":args.zip.stat().st_size,"sha256":hashlib.sha256(args.zip.read_bytes()).hexdigest()},indent=2))
