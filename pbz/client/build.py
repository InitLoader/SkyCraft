from pathlib import Path
import json
import os
import subprocess
import argparse

here=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument("--bundle",type=Path,required=True)
parser.add_argument("--base-mod",type=Path,required=True)
parser.add_argument("--output",type=Path,default=here.parent/"build/PBZCraft.jar")
args=parser.parse_args()
bundle=args.bundle.resolve()
config=json.loads((bundle/"client.json").read_text(encoding="utf-8-sig"))
classpath=[bundle/"minecraft/client.jar",args.base_mod.resolve()]
classpath.extend(bundle/"minecraft"/name for name in config["libraryPaths"])
classpath.extend((bundle/"minecraft/libraries").rglob("*mixin*.jar"))
subprocess.run([str(bundle/"runtime/bin/javac.exe"),"-proc:none","-encoding","UTF-8","-classpath",os.pathsep.join(map(str,classpath)),"-d",str(here/"classes"),*[str(path) for path in here.glob("*.java")]],check=True)
subprocess.run([os.sys.executable,str(here/"package.py"),"--base-mod",str(args.base_mod),"--output",str(args.output)],check=True)
