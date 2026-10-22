"""Build a public, standalone Windows package without local editor plugins.

Run after windows_prepare_templates.py. The source project and user editor
preferences are not edited; each export has an isolated project/cache/runtime.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BENIGN_EDITOR_EXIT = 'EditorSettings not instantiated yet when getting setting "export/android/shutdown_adb_on_exit"'


def link_or_copy(source: Path, target: Path):
    try:
        os.link(source, target)
    except OSError:
        shutil.copy2(source, target)


def run(command: list[str], path: Path, expected: str | None = None):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as output:
        result = subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT,
                                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    text = path.read_text(encoding="utf-8", errors="replace")
    errors = [line for line in text.splitlines() if "ERROR:" in line and BENIGN_EDITOR_EXIT not in line]
    if result.returncode or errors or (expected and expected not in text):
        print(text[-12000:])
        raise SystemExit(f"FAILED {path.name} exit={result.returncode}, errors={len(errors)}")
    print("OK", path.name, flush=True)
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refresh", action="store_true")
    parser.add_argument("--checks", action="store_true")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--package-only", action="store_true", help="Package an existing verified export without rebuilding")
    parser.add_argument("--name", default="windows-public-check")
    arguments = parser.parse_args()
    if not re.fullmatch(r"[a-zA-Z0-9_-]+", arguments.name):
        raise SystemExit("Invalid isolated project name")
    source = ROOT / "airwar-together"
    project = ROOT / ".tools" / arguments.name / "airwar-together"
    runtime = ROOT / ".tools" / ("windows-godot-" + arguments.name)
    runtime.mkdir(parents=True, exist_ok=True)
    for item in (ROOT / ".tools" / "Godot_v4.7.2-stable_mono_win64").iterdir():
        target = runtime / item.name
        if target.exists():
            continue
        if item.is_dir():
            shutil.copytree(item, target, copy_function=link_or_copy)
        else:
            link_or_copy(item, target)
    (runtime / "_sc_").touch()
    data = runtime / "editor_data"
    data.mkdir(exist_ok=True)
    (data / "editor_settings-4.7.tres").write_text('[gd_resource type="EditorSettings" format=3]\n\n[resource]\n', encoding="utf-8")
    templates = ROOT / ".tools" / "windows-templates" / "4.7.2.stable.mono"
    target_templates = data / "export_templates" / templates.name
    target_templates.mkdir(parents=True, exist_ok=True)
    for item in templates.iterdir():
        target = target_templates / item.name
        if item.is_file() and not target.exists():
            link_or_copy(item, target)
    if not project.exists() or arguments.refresh:
        shutil.copytree(source, project, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns(".godot", "addons", ".claude", ".agents", ".idea", ".env", ".env.example", "local.props", "bin", "obj"))
        config = project / "project.godot"
        config.write_text(re.sub(r'\[editor_plugins\]\s*enabled=.*?\n', '', config.read_text(encoding="utf-8")), encoding="utf-8")
    print("WINDOWS_EXPORT_PROJECT", project, flush=True)
    if arguments.prepare_only:
        return
    godot = runtime / "Godot_v4.7.2-stable_mono_win64_console.exe"
    logs = ROOT / "artifacts" / "windows"
    output = ROOT / "builds" / "windows"
    output.mkdir(parents=True, exist_ok=True)
    executable = output / "AirwarTogether.exe"
    if not arguments.package_only:
        run(["dotnet", "build", str(project / "airwar-together.csproj"), "--nologo"], logs / "build.log")
        run([str(godot), "--headless", "--path", str(project), "--editor", "--import", "--quit"], logs / "import.log")
        run([str(godot), "--headless", "--path", str(project), "--export-debug", "Windows", str(executable)], logs / "export.log")
    elif not executable.exists():
        raise SystemExit("No Windows export exists to package")
    if arguments.checks:
        console = output / "AirwarTogether.console.exe"
        launcher = console if console.exists() else executable
        for flag, marker in [("--self-test", "SELF_TEST_OK"), ("--asset-test", "ASSET_TEST_OK"),
                             ("--arena-self-test", "ARENA_SELF_TEST PASS"), ("--arena-asset-test", "ARENA_ASSET_OK"),
                             ("--arena-workflow-test", "ARENA_WORKFLOW_OK")]:
            run([str(launcher), "--headless", "--", flag], logs / (flag[2:] + ".log"), marker)
    for name in ["play.md", "orbital-frontier.md", "third-party-assets.md", "windows-verification.md"]:
        if not (ROOT / "docs" / name).exists():
            continue
        shutil.copy2(ROOT / "docs" / name, output / name.upper())
    shutil.copy2(ROOT / "README.md", output / "README.md")
    shutil.copy2(source / "assets/fonts/LICENSE.txt", output / "FONT-LICENSE.txt")
    version = re.search(r'^config/version="([\d.]+)"$', (source / "project.godot").read_text(encoding="utf-8"), re.MULTILINE).group(1)
    archive = ROOT / "builds" / ("AirwarTogether-windows-" + version + ".zip")
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as target:
        for item in output.rglob("*"):
            if item.is_file():
                target.write(item, "AirwarTogether/" + item.relative_to(output).as_posix())
    check_markers = {"self-test": "SELF_TEST_OK", "asset-test": "ASSET_TEST_OK", "arena-self-test": "ARENA_SELF_TEST PASS",
                     "arena-asset-test": "ARENA_ASSET_OK", "arena-workflow-test": "ARENA_WORKFLOW_OK"}
    def validated_log(name, marker):
        path = logs / (name + ".log")
        if not path.exists():
            return False
        text = path.read_text(encoding="utf-8", errors="replace")
        return marker in text and not any("ERROR:" in line and BENIGN_EDITOR_EXIT not in line for line in text.splitlines())
    check_results = {name: validated_log(name, marker) for name, marker in check_markers.items()}
    report = {"timestamp": time.strftime("%Y-%m-%d %H:%M:%S"), "version": version,
              "project": str(project), "executable": str(executable), "executable_bytes": executable.stat().st_size,
              "archive": str(archive), "archive_bytes": archive.stat().st_size, "checks": check_results,
              "executable_sha256": hashlib.file_digest(executable.open("rb"), "sha256").hexdigest(),
              "archive_sha256": hashlib.file_digest(archive.open("rb"), "sha256").hexdigest(),
              "local_plugins_excluded": not (project / "addons").exists() and not (project / "local.props").exists()}
    (logs / "release-result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("WINDOWS_RELEASE_READY", archive, flush=True)


if __name__ == "__main__":
    main()
