"""Install only Android files from the official Godot .NET export-template ZIP.

Uses HTTP byte ranges so desktop templates do not need to be downloaded. Archive
entry CRCs are verified by zipfile. Does not alter Godot editor preferences.
"""
from __future__ import annotations

import argparse
import io
import json
import os
from pathlib import Path
import shutil
import time
import zipfile

import requests


class RangeFile(io.RawIOBase):
    def __init__(self, url: str):
        self.session = requests.Session()
        response = self.session.head(url, allow_redirects=True, timeout=(15, 45))
        response.raise_for_status()
        self.url = response.url
        self.size = int(response.headers["Content-Length"])
        self.pos = 0
        self.fetched = 0
        self.cached_start = 0
        self.cached = b""

    def seekable(self):
        return True

    def readable(self):
        return True

    def tell(self):
        return self.pos

    def seek(self, offset: int, whence: int = 0):
        self.pos = offset if whence == 0 else self.pos + offset if whence == 1 else self.size + offset
        if self.pos < 0:
            raise ValueError("Negative seek")
        return self.pos

    def read(self, size: int = -1):
        end = self.size if size < 0 else min(self.size, self.pos + size)
        if end <= self.pos:
            return b""
        start = self.pos
        if start >= self.cached_start and end <= self.cached_start + len(self.cached):
            self.pos = end
            return self.cached[start-self.cached_start:end-self.cached_start]
        fetch_end = min(self.size, max(end, start + 8 * 1024 * 1024)) if size >= 4096 else end
        for attempt in range(4):
            try:
                response = self.session.get(self.url, headers={"Range": f"bytes={start}-{fetch_end-1}"}, timeout=(15, 90))
                response.raise_for_status()
                if response.status_code != 206:
                    raise IOError(f"Server ignored HTTP Range ({response.status_code})")
                value = response.content
                if len(value) != fetch_end - start:
                    raise IOError(f"Range length mismatch: {len(value)} / {fetch_end-start}")
                self.pos = end
                self.fetched += len(value)
                self.cached_start = start
                self.cached = value
                return value[:end-start]
            except (requests.RequestException, IOError):
                if attempt == 3:
                    raise
                time.sleep(1 + attempt)
        raise AssertionError("unreachable")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default="4.7.2")
    parser.add_argument("--destination", type=Path)
    parser.add_argument("--list", action="store_true")
    arguments = parser.parse_args()
    destination = arguments.destination or Path(os.environ["APPDATA"]) / "Godot" / "export_templates" / f"{arguments.version}.stable.mono"
    url = f"https://github.com/godotengine/godot-builds/releases/download/{arguments.version}-stable/Godot_v{arguments.version}-stable_mono_export_templates.tpz"
    remote = RangeFile(url)
    with zipfile.ZipFile(remote) as archive:
        entries = archive.infolist()
        if arguments.list:
            for info in entries:
                print(info.filename, info.file_size, info.compress_size)
            return
        selected = [info for info in entries if not info.is_dir() and
                    (Path(info.filename).name.startswith("android") or Path(info.filename).name == "version.txt")]
        if not selected:
            raise SystemExit("Archive contains no Android templates")
        destination.mkdir(parents=True, exist_ok=True)
        report = []
        for info in selected:
            name = Path(info.filename).name
            target = destination / name
            if target.exists() and target.stat().st_size == info.file_size:
                print(f"EXISTS {name}", flush=True)
                continue
            partial = target.with_suffix(target.suffix + ".partial")
            print(f"FETCH {name} ({info.compress_size / 1048576:.1f} MiB)", flush=True)
            with archive.open(info) as source, partial.open("wb") as output:
                shutil.copyfileobj(source, output, length=1024 * 1024)
            partial.replace(target)
            report.append({"file": name, "bytes": info.file_size, "crc32": f"{info.CRC:08x}"})
            print(f"INSTALLED {target}", flush=True)
        (destination / "android-template-source.json").write_text(json.dumps({"source": url, "archive_bytes": remote.size,
            "downloaded_bytes": remote.fetched, "files": report}, indent=2), encoding="utf-8")
        print(f"TEMPLATES_READY {destination} ({remote.fetched / 1048576:.1f} MiB transferred)")


if __name__ == "__main__":
    main()
