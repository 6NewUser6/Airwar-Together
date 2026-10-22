"""Verify exported APK architecture, networking, scene dependencies and source exclusions."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--architecture", choices=("arm64-v8a", "x86_64"), required=True)
    parser.add_argument("--sdk", type=Path, default=Path(os.environ["LOCALAPPDATA"]) / "Android" / "Sdk")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    aapt = sorted((args.sdk / "build-tools").glob("*/aapt.exe"))[-1]
    badging = subprocess.check_output([str(aapt), "dump", "badging", str(args.apk.resolve())], text=True, encoding="utf-8")
    assert "uses-permission: name='android.permission.INTERNET'" in badging, "Missing Internet permission"
    assert f"native-code: '{args.architecture}'" in badging, "Incorrect native architecture"
    assert "sdkVersion:'24'" in badging, "Unexpected minimum Android API"
    arch = "arm64" if args.architecture == "arm64-v8a" else "x86_64"
    with zipfile.ZipFile(args.apk) as archive:
        names = archive.namelist()
        assert f"lib/{args.architecture}/libgodot_android.so" in names, "Missing native Godot runtime"
        assert f"assets/.godot/mono/publish/{arch}/airwar-together.dll" in names, "Missing game assembly"
        collision = json.loads(archive.read("assets/assets/arena/collision_world.json"))
        assert collision, "Empty collision world"
        assert any("assets/community/" in n and "LICENSE" in n for n in names), "Missing community asset licenses"
        assert not any("godot_mcp" in n.lower() or "gamedev-mcp" in n.lower() for n in names), "Local MCP files leaked into APK"
        assert not any("参考项目" in n for n in names), "Reference project leaked into APK"
        assert not any(n.lower().endswith("local.props") or n.endswith("/.env") for n in names), "Local configuration leaked into APK"
    report = {"apk": str(args.apk.resolve()), "bytes": args.apk.stat().st_size,
              "sha256": hashlib.file_digest(args.apk.open("rb"), "sha256").hexdigest(),
              "architecture": args.architecture, "internet": True, "collision_world": True,
              "community_licenses": True, "local_mcp_excluded": True}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("ANDROID_APK_CHECK_OK", json.dumps(report))


if __name__ == "__main__":
    main()
