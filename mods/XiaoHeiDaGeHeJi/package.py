"""Build the plugin and assemble the local r2modman ZIP. Requires Python 3 and the .NET SDK."""
import json
from pathlib import Path
import subprocess
import zipfile

root = Path(__file__).resolve().parent
package = root / "package"
manifest = json.loads((package / "manifest.json").read_text(encoding="utf-8"))
project = root / "src" / "RiftSurvivor.csproj"
subprocess.run(["dotnet", "build", str(project), "-c", "Release", "--nologo"], check=True)
dll = root / "src" / "bin" / "Release" / "netstandard2.1" / "RiftSurvivor.dll"
out = root / "dist" / f"LocalMods-{manifest['name']}-{manifest['version_number']}.zip"
out.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as archive:
    for name in ("manifest.json", "README.md", "icon.png"):
        archive.write(package / name, name)
    archive.write(dll, f"plugins/{dll.name}")
print(out)
