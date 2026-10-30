from pathlib import Path
import pefile
import argparse

root = Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument("dll",type=Path)
dll=parser.parse_args().dll
pe = pefile.PE(str(dll))
names = [s.name.decode("ascii") for s in pe.DIRECTORY_ENTRY_EXPORT.symbols if s.name]
(root / "UE4SS.def").write_text("LIBRARY UE4SS.dll\nEXPORTS\n" + "\n".join(names) + "\n", encoding="ascii")
print(f"Wrote {len(names)} actual exports from installed UE4SS.dll")
