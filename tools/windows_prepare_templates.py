"""Fetch only Windows x86_64 templates from the official Godot .NET archive.

Stores templates in the workspace, never changing user Godot settings. ZIP entry
CRCs are checked by zipfile while decompressing HTTP byte ranges.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import zipfile

from android_prepare_templates import RangeFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default="4.7.2")
    parser.add_argument("--destination", type=Path)
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    destination = arguments.destination or root / ".tools/windows-templates" / (arguments.version + ".stable.mono")
    destination.mkdir(parents=True, exist_ok=True)
    url = f"https://github.com/godotengine/godot-builds/releases/download/{arguments.version}-stable/Godot_v{arguments.version}-stable_mono_export_templates.tpz"
    remote = RangeFile(url)
    installed = []
    with zipfile.ZipFile(remote) as archive:
        wanted = {"version.txt", "windows_debug_x86_64.exe", "windows_debug_x86_64_console.exe",
                  "windows_release_x86_64.exe", "windows_release_x86_64_console.exe"}
        for info in archive.infolist():
            name = Path(info.filename).name
            if name not in wanted:
                continue
            target = destination / name
            if target.exists() and target.stat().st_size == info.file_size:
                print("EXISTS", name, flush=True)
            else:
                partial = target.with_suffix(target.suffix + ".partial")
                print("FETCH", name, info.compress_size, flush=True)
                with archive.open(info) as source, partial.open("wb") as output:
                    shutil.copyfileobj(source, output, 1024 * 1024)
                partial.replace(target)
                print("INSTALLED", target, flush=True)
            installed.append({"name": name, "bytes": info.file_size, "crc32": f"{info.CRC:08x}"})
    if set(p.name for p in destination.glob("*.exe")) != wanted - {"version.txt"}:
        raise SystemExit("Missing Windows templates")
    (destination / "template-source.json").write_text(json.dumps({"source": url, "downloaded_bytes": remote.fetched,
        "archive_bytes": remote.size, "files": installed}, indent=2), encoding="utf-8")
    print("WINDOWS_TEMPLATES_READY", destination, flush=True)


if __name__ == "__main__":
    main()
