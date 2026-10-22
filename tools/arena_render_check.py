"""Run the actual GPU stress scene through Main, and preserve every run's evidence.

This uses 32 local simulated actors, not 32 network peers. No headless substitute
is accepted, and native crashes or engine errors fail even if a marker exists.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def actual_executable(path: Path) -> Path:
    # Godot's Windows console executable launches a second GUI executable. Use
    # that engine directly so timeout termination also stops the renderer.
    if os.name == "nt":
        for suffix in ("_console.exe", ".console.exe"):
            if path.name.lower().endswith(suffix):
                sibling = path.with_name(path.name[:-len(suffix)] + ".exe")
                if sibling.is_file():
                    return sibling
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", help="Godot .NET executable; defaults to GODOT_PATH or workspace tools")
    parser.add_argument("--executable", help="Test an exported game instead of the source project")
    parser.add_argument("--seconds", type=int, default=240)
    parser.add_argument("--runs", type=int, default=3)
    parser.add_argument("--renderer", choices=["gl_compatibility", "mobile", "forward_plus"])
    parser.add_argument("--driver", choices=["opengl3", "opengl3_angle", "vulkan", "d3d12"])
    parser.add_argument("--render-thread", choices=["safe", "separate"],
                        help="Override the engine render thread for a controlled comparison")
    parser.add_argument("--dotnet-roll-forward", choices=["LatestPatch", "LatestMajor"],
                        help="Select the .NET host policy for this check's child processes only")
    args = parser.parse_args()
    if not 10 <= args.seconds <= 3600 or not 1 <= args.runs <= 20:
        parser.error("seconds must be 10–3600 and runs must be 1–20")
    directory = ROOT / "artifacts/render" / datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    directory.mkdir(parents=True)
    if args.executable:
        base = [str(actual_executable(Path(args.executable).resolve()))]
    else:
        candidate = args.godot or os.environ.get("GODOT_PATH") or shutil.which("godot")
        if not candidate:
            candidate = ROOT / ".tools/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
        if not Path(candidate).is_file():
            parser.error("Pass --godot /path/to/Godot .NET")
        base = [str(actual_executable(Path(candidate).resolve())), "--path", str(ROOT / "airwar-together")]
        # Restoring files can preserve older timestamps. Rebuild prevents the
        # tested DLL from silently retaining an earlier experiment's code.
        with (directory / "build.log").open("w", encoding="utf-8") as output:
            build = subprocess.run(["dotnet", "build", str(ROOT / "airwar-together/airwar-together.csproj"),
                                    "-t:Rebuild", "--nologo"], cwd=ROOT, stdout=output, stderr=subprocess.STDOUT)
        if build.returncode:
            raise SystemExit(f"Build failed; see {directory / 'build.log'}")
    base += ["--audio-driver", "Dummy", "--resolution", "1280x720"]
    if args.renderer:
        base += ["--rendering-method", args.renderer]
    if args.driver:
        base += ["--rendering-driver", args.driver]
    if args.render_thread:
        base += ["--render-thread", args.render_thread]
    base += ["--disable-render-loop"]
    environment = os.environ.copy()
    if args.dotnet_roll_forward:
        environment["DOTNET_ROLL_FORWARD"] = args.dotnet_roll_forward
    def sha256(path: Path):
        with path.open("rb") as source:
            return hashlib.file_digest(source, "sha256").hexdigest()
    manifest = {"executable": base[0], "executable_sha256": sha256(Path(base[0]))}
    manifest["render_thread_override"] = args.render_thread
    manifest["dotnet_roll_forward"] = environment.get("DOTNET_ROLL_FORWARD")
    manifest["manual_draw"] = True
    manifest["swap_buffers"] = True
    if not args.executable:
        manifest["source_sha256"] = {name: sha256(ROOT / "airwar-together" / name) for name in [
            "project.godot", "airwar-together.csproj",
            "scripts/Main.cs", "scripts/Arena/ArenaWorld.cs", "scripts/Arena/ArenaRenderChecks.cs",
            "scripts/Arena/ArenaSimulation.cs", "scenes/Main.tscn", "scenes/arena/RenderCheck.tscn"]}
        assembly = ROOT / "airwar-together/.godot/mono/temp/bin/Debug/airwar-together.dll"
        manifest["assembly_sha256"] = sha256(assembly)
    (directory / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    results = []
    for index in range(1, args.runs + 1):
        prefix = directory / f"run-{index}"
        log = prefix.with_suffix(".log")
        metrics = prefix.with_suffix(".jsonl")
        command = base + ["--log-file", str(log), "--", "--arena-render-test",
                          f"--arena-duration={args.seconds}", f"--arena-metrics={metrics}",
                          "--arena-manual-draw", f"--arena-capture-directory={directory / ('frames-' + str(index))}"]
        print(f"GPU_CHECK_START {index}/{args.runs} {directory}", flush=True)
        try:
            process = subprocess.run(command, cwd=ROOT, capture_output=True,
                                     encoding="utf-8", errors="replace", timeout=args.seconds + 120,
                                     env=environment)
            code, timeout = process.returncode, False
            output = process.stdout + process.stderr
        except subprocess.TimeoutExpired as error:
            code, timeout = None, True
            output = "\n".join(part.decode("utf-8", errors="replace") if isinstance(part, bytes) else part or ""
                               for part in [error.stdout, error.stderr])
        prefix.with_suffix(".console.log").write_text(output, encoding="utf-8")
        engine_log = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
        combined = engine_log + "\n" + output
        errors = [line for line in combined.splitlines() if "ERROR:" in line]
        native_status = code is not None and os.name == "nt" and (code & 0xffffffff) in {
            0xc0000005, 0xc000001d, 0xc00000fd, 0xc0000374, 0xc0000409}
        native_crash = native_status or "crashed with signal" in combined or "C++ BACKTRACE" in combined
        samples = [json.loads(line) for line in metrics.read_text(encoding="utf-8").splitlines()] if metrics.exists() else []
        sectors = sorted({sample["sector"] for sample in samples})
        fps = [sample["fps"] for sample in samples if sample["seconds"] >= 5]
        passed = code == 0 and not timeout and not errors and not native_crash and "ARENA_RENDER_OK " in combined
        if args.seconds >= 60:
            passed = passed and sectors == [0, 1, 2, 3]
        image_hashes = {sample.get("imageHash") for sample in samples if sample.get("imageHash")}
        drawn_sectors = sorted({sample["sector"] for sample in samples
                                if sample.get("imageHash") and sample.get("drawCalls", 0) > 0})
        actual_frames = samples[-1].get("drawnFrames", 0) if samples else 0
        passed = passed and len(image_hashes) >= 2 and actual_frames >= args.seconds * 5
        if args.seconds >= 60:
            passed = passed and drawn_sectors == [0, 1, 2, 3]
        captures = directory / ("frames-" + str(index))
        capture_sectors = sorted(int(path.stem.split("-")[-1]) for path in captures.glob("sector-*.png"))
        if args.seconds >= 60:
            passed = passed and capture_sectors == [0, 1, 2, 3]
        result = {"run": index, "passed": passed, "exit_code": code, "timeout": timeout,
                  "native_crash": native_crash, "errors": sorted(set(errors)), "sectors": sectors,
                  "samples": len(samples), "last_seconds": samples[-1]["seconds"] if samples else 0,
                  "dotnet_runtimes": sorted({sample.get("runtime", "unrecorded") for sample in samples}),
                  "image_hashes": len(image_hashes),
                  "drawn_sectors": drawn_sectors, "drawn_frames": actual_frames,
                  "capture_sectors": capture_sectors,
                  "fps_min_after_startup": min(fps) if fps else None,
                  "fps_max_after_startup": max(fps) if fps else None,
                  "log": str(log), "metrics": str(metrics), "command": command}
        results.append(result)
        report = {"scope": "actual GPU, Main-to-arena transition, 32 local simulated actors",
                  "network_capacity_test": False, "requested_seconds": args.seconds,
                  "requested_runs": args.runs, "completed_runs": len(results),
                  "passed": len(results) == args.runs and all(item["passed"] for item in results), "runs": results}
        (directory / "result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("GPU_CHECK_" + ("PASS" if passed else "FAIL"), index, "exit=", code, flush=True)
    print("GPU_CHECK_REPORT", directory / "result.json", flush=True)
    return 0 if all(item["passed"] for item in results) else 1


if __name__ == "__main__":
    sys.exit(main())
