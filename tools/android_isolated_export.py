"""Export a public project copy with isolated Godot/.NET settings and no local MCP.

Copies current source and assets, leaving the user's live editor cache untouched.
Only the copy's architecture flags are changed when --emulator is requested.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]


def link_or_copy(source, target):
    try:
        os.link(source, target)
    except OSError:
        shutil.copy2(source, target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--emulator", action="store_true")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--refresh", action="store_true")
    parser.add_argument("--name", default="android-public-check")
    parser.add_argument("--test-args", default="", help="Validation-only engine/user arguments embedded in the copied preset")
    parser.add_argument("--suffix", default="", help="Optional output filename suffix for a validation APK")
    args = parser.parse_args()
    source = ROOT / "airwar-together"
    project = ROOT / ".tools" / args.name / "airwar-together"
    # Godot names temporary APKs by timestamp inside editor_data. A distinct
    # self-contained runtime per copy prevents simultaneous exports colliding.
    if not re.fullmatch(r'[a-zA-Z0-9_-]+', args.name):
        raise SystemExit("Invalid isolated project name")
    runtime = ROOT / ".tools" / ("android-godot-" + args.name)
    dotnet = ROOT / ".tools" / "dotnet-android"
    sdk = Path(os.environ["LOCALAPPDATA"]) / "Android" / "Sdk"
    java = Path(os.environ.get("JAVA_HOME", str(Path.home() / ".jdks" / "corretto-21.0.6")))
    editor = ROOT / ".tools" / "Godot_v4.7.2-stable_mono_win64"
    runtime.mkdir(parents=True, exist_ok=True)
    for item in editor.iterdir():
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
    settings = {
        "export/android/android_sdk_path": str(sdk),
        "export/android/java_sdk_path": str(java),
        "export/android/debug_keystore": str(Path(os.environ["APPDATA"]) / "Godot" / "keystores" / "debug.keystore"),
        "export/android/debug_keystore_user": "androiddebugkey",
        "export/android/debug_keystore_pass": "android",
        "dotnet/dotnet_root": str(dotnet),
    }
    (data / "editor_settings-4.7.tres").write_text('[gd_resource type="EditorSettings" format=3]\n\n[resource]\n' +
        "\n".join(f'{key} = "{value.replace(chr(92), "/")}"' for key, value in settings.items()) + "\n", encoding="utf-8")
    templates = Path(os.environ["APPDATA"]) / "Godot" / "export_templates" / "4.7.2.stable.mono"
    target_templates = data / "export_templates" / templates.name
    target_templates.mkdir(parents=True, exist_ok=True)
    for item in templates.iterdir():
        target = target_templates / item.name
        if item.is_file() and not target.exists():
            link_or_copy(item, target)
    if not project.exists() or args.refresh:
        shutil.copytree(source, project, dirs_exist_ok=True,
            ignore=shutil.ignore_patterns(".godot", "addons", ".claude", ".idea", ".env", ".env.example", "local.props", "bin", "obj"))
        config = project / "project.godot"
        config.write_text(re.sub(r'\[editor_plugins\]\s*enabled=.*?\n', '', config.read_text(encoding="utf-8")), encoding="utf-8")
    presets = project / "export_presets.cfg"
    text = presets.read_text(encoding="utf-8")
    text = re.sub(r'^architectures/arm64-v8a=(true|false)$',
                  f'architectures/arm64-v8a={str(not args.emulator).lower()}', text, flags=re.MULTILINE)
    text = re.sub(r'^architectures/x86_64=(true|false)$',
                  f'architectures/x86_64={str(args.emulator).lower()}', text, flags=re.MULTILINE)
    text = text.replace('permissions/internet=false', 'permissions/internet=true')
    text = re.sub(r'^command_line/extra_args=.*\n?', '', text, flags=re.MULTILINE)
    if args.test_args:
        if '"' in args.test_args or '\n' in args.test_args:
            raise SystemExit("Test arguments cannot contain quotes or newlines")
        text = text.replace('[preset.2.options]', '[preset.2.options]\n\ncommand_line/extra_args="' + args.test_args + '"')
    presets.write_text(text, encoding="utf-8")
    print("ANDROID_EXPORT_PROJECT", project, flush=True)
    if args.prepare_only:
        return
    env = os.environ.copy()
    env.update({"DOTNET_ROOT": str(dotnet), "DOTNET_MULTILEVEL_LOOKUP": "0", "ANDROID_HOME": str(sdk), "ANDROID_SDK_ROOT": str(sdk),
                "JAVA_HOME": str(java), "PATH": str(dotnet) + os.pathsep + env["PATH"]})
    log_dir = ROOT / "artifacts" / "android"
    log_dir.mkdir(parents=True, exist_ok=True)
    godot = runtime / "Godot_v4.7.2-stable_mono_win64_console.exe"
    tag = "emulator" if args.emulator else "arm64"
    if args.suffix:
        if not re.fullmatch(r'[a-zA-Z0-9_-]+', args.suffix):
            raise SystemExit("Invalid filename suffix")
        tag += "-" + args.suffix
    output = ROOT / "builds" / "android" / ("AirwarTogether-" + tag + ".apk")
    output.parent.mkdir(parents=True, exist_ok=True)
    commands = [
        ("build", [str(dotnet / "dotnet.exe"), "build", str(project / "airwar-together.csproj"), "--nologo"]),
        ("import", [str(godot), "--headless", "--path", str(project), "--editor", "--import", "--quit"]),
        ("export", [str(godot), "--headless", "--path", str(project), "--export-debug", "Android", str(output)]),
    ]
    for name, command in commands:
        path = log_dir / (f"{args.name}-{name}.log")
        print("RUN", name, flush=True)
        with path.open("w", encoding="utf-8") as log:
            result = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        output_text = path.read_text(encoding="utf-8", errors="replace")
        errors = [line for line in output_text.splitlines() if "ERROR:" in line and
                  'EditorSettings not instantiated yet when getting setting "export/android/shutdown_adb_on_exit"' not in line]
        if result.returncode or errors:
            print(output_text[-12000:])
            raise SystemExit(f"{name} failed ({result.returncode}); {path}")
        print("OK", name, path, flush=True)
    print("ANDROID_APK_READY", output, output.stat().st_size, flush=True)


if __name__ == "__main__":
    main()
