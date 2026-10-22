"""Create a signed validation-only APK variant with different Godot arguments.

Only assets/_cl_ changes. Project assemblies, scenes and imported resources stay
byte-identical. Never use validation arguments for a store/release package.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import struct
import subprocess
import zipfile


def unpack(data: bytes):
    count = struct.unpack_from("<I", data)[0]
    result, offset = [], 4
    for _ in range(count):
        size = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        result.append(data[offset:offset+size].decode("utf-8"))
        offset += size
    assert offset == len(data)
    return result


def pack(arguments):
    encoded = [argument.encode("utf-8") for argument in arguments]
    return struct.pack("<I", len(encoded)) + b"".join(struct.pack("<I", len(item)) + item for item in encoded)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--argument", action="append", default=[])
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve():
        raise SystemExit("Validation output must differ from the original APK")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    unaligned = args.output.with_suffix(".unaligned.apk")
    with zipfile.ZipFile(args.source) as source, zipfile.ZipFile(unaligned, "w") as target:
        original = unpack(source.read("assets/_cl_"))
        engine = original[:original.index("--")] if "--" in original else original
        arguments = engine + (["--"] + args.argument if args.argument else [])
        for info in source.infolist():
            if info.filename.startswith("META-INF/"):
                continue
            target.writestr(info, pack(arguments) if info.filename == "assets/_cl_" else source.read(info))
    sdk = Path(os.environ["LOCALAPPDATA"]) / "Android" / "Sdk"
    tools = sorted((sdk / "build-tools").iterdir())[-1]
    subprocess.run([str(tools / "zipalign.exe"), "-P", "16", "-f", "4", str(unaligned), str(args.output)], check=True)
    keystore = Path(os.environ["APPDATA"]) / "Godot" / "keystores" / "debug.keystore"
    subprocess.run([str(tools / "apksigner.bat"), "sign", "--ks", str(keystore), "--ks-key-alias", "androiddebugkey",
                    "--ks-pass", "pass:android", str(args.output)], check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    subprocess.run([str(tools / "apksigner.bat"), "verify", str(args.output)], check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    unaligned.unlink()
    print("VALIDATION_APK_READY", args.output, arguments)


if __name__ == "__main__":
    main()
