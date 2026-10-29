from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json
import argparse
import hashlib

root = Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument("--base-mod",type=Path,required=True)
parser.add_argument("--output",type=Path,required=True)
args=parser.parse_args()
source=args.base_mod
target=args.output
profile=json.loads((root/"profile.json").read_text(encoding="utf-8"))
if hashlib.sha256(source.read_bytes()).hexdigest().upper()!=profile["BaseModSHA256"]:
    raise SystemExit("The base bridge JAR differs from the tested client.")
target.parent.mkdir(parents=True,exist_ok=True)
with ZipFile(source) as old, ZipFile(target, "w", ZIP_DEFLATED) as new:
    for entry in old.infolist():
        data = old.read(entry.filename)
        if entry.filename == "fabric.mod.json":
            metadata = json.loads(data)
            metadata.update(id="pbzcraft", name="PBZCraft", version="0.0.1-pbz-trial")
            metadata["entrypoints"]["client"].append("dev.pbzcraft.PBZDiagnostics")
            metadata["mixins"].append("pbzcraft.client.mixins.json")
            data = json.dumps(metadata, ensure_ascii=False, indent=2).encode("utf-8")
        if entry.filename == "skycraft.client.mixins.json":
            mixins = json.loads(data)
            mixins["client"].remove("EntityCollideMixin")
            data = json.dumps(mixins, indent=2).encode("utf-8")
        new.writestr(entry, data)
    new.writestr("pbzcraft.client.mixins.json", json.dumps({"required": True, "package": "dev.pbzcraft.mixin", "compatibilityLevel": "JAVA_25", "client": ["PBZEntityCollideMixin"], "injectors": {"defaultRequire": 1}}, indent=2))
    for file in (root / "client/classes").rglob("*.class"):
        new.write(file, file.relative_to(root / "client/classes").as_posix())
print("Packaged isolated PBZCraft client diagnostics; original bridge classes preserved")
