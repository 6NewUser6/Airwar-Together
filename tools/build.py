#!/usr/bin/env python3
"""Build the Godot .NET game and package local test releases."""
import argparse
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "airwar-together"


def find_godot(explicit):
    candidates = [explicit, os.environ.get("GODOT_PATH"), shutil.which("godot"), shutil.which("godot4")]
    candidates += list(ROOT.parent.glob("Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return str(Path(candidate).resolve())
    raise SystemExit("Set GODOT_PATH or pass --godot /absolute/path/to/Godot .NET")


def run(command, name, expected=None):
    log = ROOT / "artifacts" / "logs" / (name + ".log")
    log.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
    output = result.stdout + result.stderr
    log.write_text(output, encoding="utf-8")
    # A Godot 4.7.2 editor shutdown diagnostic occurs after successful CLI export.
    errors = [line for line in output.splitlines() if "ERROR:" in line and "EditorSettings not instantiated yet when getting setting \"export/android/shutdown_adb_on_exit\"" not in line]
    if result.returncode or errors or (expected and expected not in output):
        print(output)
        raise SystemExit(f"{name} failed; see {log}")
    print(f"OK {name} ({log.relative_to(ROOT)})", flush=True)


def package(platform):
    directory = ROOT / "builds" / platform.lower()
    shutil.copy2(PROJECT / "assets/fonts/LICENSE.txt", directory / "FONT-LICENSE.txt")
    shutil.copy2(ROOT / "docs/play.md", directory / "PLAY.md")
    if platform == "Android":
        return
    version = re.search(r'^config/version="([\d.]+)"$', (PROJECT / "project.godot").read_text(), re.MULTILINE)
    if not version:
        raise SystemExit("Missing project version")
    archive = ROOT / "builds" / f"AirwarTogether-{platform.lower()}-{version.group(1)}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as target:
        for source in directory.rglob("*"):
            if source.is_file():
                target.write(source, "AirwarTogether/" + source.relative_to(directory).as_posix())
    print(f"PACKAGE {archive} ({archive.stat().st_size / 1048576:.1f} MiB)", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot")
    parser.add_argument("--platform", choices=["Linux", "Windows", "Android"], action="append")
    parser.add_argument("--checks", action="store_true", help="Run battle and persistence checks before export")
    args = parser.parse_args()
    godot = find_godot(args.godot)
    run(["dotnet", "build", str(PROJECT / "airwar-together.csproj"), "--nologo"], "build")
    if args.checks:
        run([godot, "--headless", "--path", str(PROJECT), "--", "--self-test"], "core-checks", "SELF_TEST_OK")
    for platform in args.platform or ["Linux", "Windows", "Android"]:
        (ROOT / "builds" / platform.lower()).mkdir(parents=True, exist_ok=True)
        run([godot, "--headless", "--path", str(PROJECT), "--export-debug", platform], "export-" + platform.lower())
        package(platform)


if __name__ == "__main__":
    main()
