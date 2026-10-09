from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import argparse
import hashlib
import json
import shutil


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


parser = argparse.ArgumentParser(description='Package the verified DuckovCraft 0.1.3 pair without games, runtimes or saves.')
parser.add_argument('--game-root', type=Path, required=True)
parser.add_argument('--stage-dir', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
mod = args.game_root / 'Duckov_Data/Mods/DuckovCraft'
mc = args.game_root / 'DuckovCraft/minecraft/mods'
assert 'version = 0.1.3' in (mod / 'info.ini').read_text(encoding='utf-8')
with ZipFile(mc / 'sulfurcraft.jar') as jar:
    metadata = json.loads(jar.read('fabric.mod.json'))
    assert metadata['version'] == '0.1.2-sulfur.4'
    assert metadata['depends']['minecraft'] == '~26.3'
    assert jar.read('skycraft-host.properties').decode().strip() == 'host=sulfur'
    assert jar.testzip() is None
api = mc / 'fabric-api-0.162.0+26.3.jar'
with ZipFile(api) as jar:
    assert json.loads(jar.read('fabric.mod.json'))['version'] == '0.162.0+26.3'
    api_license = jar.read('LICENSE-fabric-api')
    assert jar.testzip() is None

args.stage_dir.mkdir(parents=True, exist_ok=False)
files = {
    'Minecraft端/mods/sulfurcraft.jar': mc / 'sulfurcraft.jar',
    'Minecraft端/mods/fabric-api-0.162.0+26.3.jar': api,
    '安装教程.md': repo / 'duckov/MODS-INSTALL.zh-CN.md',
    '许可证/SkyCraft-MIT.txt': repo / 'LICENSE',
}
for name in ('DuckovCraft.dll', 'info.ini', 'duckovcraft-assets'):
    files[f'鸭科夫端/Duckov_Data/Mods/DuckovCraft/{name}'] = mod / name
for relative, source in files.items():
    target = args.stage_dir / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    assert sha256(target) == sha256(source)
(args.stage_dir / '许可证/Fabric-API-Apache-2.0.txt').write_bytes(api_license)
payload = sorted(p for p in args.stage_dir.rglob('*') if p.is_file())
checksums = ''.join(f'{sha256(p)}  {p.relative_to(args.stage_dir).as_posix()}\n' for p in payload)
(args.stage_dir / '文件校验.sha256').write_text(checksums, encoding='utf-8-sig')
args.output.parent.mkdir(parents=True, exist_ok=True)
assert not args.output.exists()
with ZipFile(args.output, 'w', ZIP_DEFLATED, compresslevel=9) as archive:
    for source in sorted(p for p in args.stage_dir.rglob('*') if p.is_file()):
        archive.write(source, source.relative_to(args.stage_dir).as_posix())
with ZipFile(args.output) as archive:
    assert archive.testzip() is None
    assert len(archive.namelist()) == 9
    for source in args.stage_dir.rglob('*'):
        if source.is_file():
            assert archive.read(source.relative_to(args.stage_dir).as_posix()) == source.read_bytes()
args.output.with_name(args.output.name + '.sha256').write_text(f'{sha256(args.output)}  {args.output.name}\n', encoding='utf-8')
print(json.dumps({'package': str(args.output), 'bytes': args.output.stat().st_size, 'files': 9, 'sha256': sha256(args.output), 'game_files': False, 'saves': False, 'personal_settings': False}, ensure_ascii=False))
