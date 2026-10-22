"""Verify one Android APK and 31 desktop processes share a real 32-seat ENet room.

Requires an already booted Android emulator. The Android game reaches the host
through the emulator's 10.0.2.2 alias. This does not replace phone/Wi-Fi testing.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time


ROOT = Path(__file__).resolve().parents[1]


def records(path):
    if not path.exists():
        return []
    result = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            result.append(json.loads(line))
        except json.JSONDecodeError:
            pass
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", type=Path, required=True)
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--device", default="emulator-5556")
    parser.add_argument("--port", type=int, default=24698)
    parser.add_argument("--duration", type=int, default=90)
    args = parser.parse_args()
    adb = Path(os.environ["LOCALAPPDATA"]) / "Android/Sdk/platform-tools/adb.exe"
    device = [str(adb), "-s", args.device]
    if subprocess.check_output(device + ["shell", "getprop", "sys.boot_completed"], text=True).strip() != "1":
        raise SystemExit("Android has not completed boot")
    output = ROOT / "artifacts/android" / ("mixed-" + time.strftime("%Y%m%d-%H%M%S"))
    output.mkdir(parents=True)
    variant = output / "AirwarTogether-mixed-client.apk"
    subprocess.run([sys.executable, str(ROOT / "tools/android_validation_args.py"), str(args.apk), str(variant),
        "--argument=--arena-client=10.0.2.2", f"--argument=--arena-port={args.port}", "--argument=--arena-diagnostics",
        f"--argument=--arena-duration={args.duration}"], check=True)
    subprocess.run(device + ["install", "--no-incremental", "-r", str(variant)], check=True)
    subprocess.run(device + ["logcat", "-c"], check=True)
    handles = []
    engine = args.godot.resolve()
    if engine.name.endswith("_console.exe"):
        direct_engine = engine.with_name(engine.name.replace("_console.exe", ".exe"))
        if direct_engine.exists():
            engine = direct_engine
    files = []
    extra = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}

    def launch(name, mode, index=0):
        file = (output / (name + ".log")).open("w", encoding="utf-8")
        files.append(file)
        command = [str(engine), "--headless", "--path", str(ROOT / "airwar-together"),
            "res://scenes/arena/NetworkCheck.tscn", "--", f"--arena-network-test={mode}", f"--arena-index={index}",
            f"--arena-duration={args.duration+100}", f"--arena-port={args.port}", f"--arena-report={output / (name+'.jsonl')}"]
        process = subprocess.Popen(command, stdout=file, stderr=subprocess.STDOUT, **extra)
        handles.append(process)
        return process

    def wait(predicate, seconds, description):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if predicate():
                return
            if server.poll() is not None:
                raise RuntimeError("Server terminated: " + str(server.returncode))
            time.sleep(.5)
        raise TimeoutError(description)

    try:
        server = launch("server", "server")
        wait(lambda: any(r.get("type") == "listening" for r in records(output / "server.jsonl")), 20, "server listening")
        for index in range(31):
            launch(f"desktop-{index:02}", "client", index)
            time.sleep(.12)
        wait(lambda: any(r.get("players") == 31 for r in records(output / "server.jsonl")), 45, "31 desktop members")
        android_log = (output / "android.log").open("w", encoding="utf-8")
        files.append(android_log)
        logcat = subprocess.Popen(device + ["logcat", "-v", "brief", "-s", "godot", "Godot", "AndroidRuntime"],
            stdout=android_log, stderr=subprocess.STDOUT, **extra)
        handles.append(logcat)
        subprocess.run(device + ["shell", "am", "start", "-n", "com.airwar.together/com.godot.game.GodotAppLauncher"], check=True)

        def android_metrics():
            result = []
            for line in (output / "android.log").read_text(encoding="utf-8", errors="replace").splitlines():
                if "ARENA_METRICS {" in line:
                    try:
                        result.append(json.loads(line.split("ARENA_METRICS ", 1)[1]))
                    except json.JSONDecodeError:
                        pass
            return result

        wait(lambda: any(m.get("players") == 32 and m.get("received", 0) > 0 for m in android_metrics()), 60, "Android sees 32 members")
        wait(lambda: any(r.get("players") == 32 for r in records(output / "server.jsonl")), 10, "Server sees mixed 32 members")
        time.sleep(10)
        metrics = android_metrics()
        states = [r for r in records(output / "server.jsonl") if r.get("type") == "server"]
        report = {"passed": True, "desktop_clients": 31, "android_clients": 1,
            "server_peak": max(r.get("players", 0) for r in states), "android_peak": max(m.get("players", 0) for m in metrics),
            "server_shots": max(r.get("shots", 0) for r in states), "server_warps": max(r.get("warps", 0) for r in states),
            "android_received": max(m.get("received", 0) for m in metrics), "android_metrics": metrics,
            "device": args.device, "transport": "ENet UDP via Android emulator host alias 10.0.2.2"}
        (output / "result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("ANDROID_MIXED_ROOM_OK", json.dumps({k: v for k, v in report.items() if k != "android_metrics"}), flush=True)
        return 0
    except Exception as error:
        (output / "result.json").write_text(json.dumps({"passed": False, "error": str(error)}, indent=2), encoding="utf-8")
        print("ANDROID_MIXED_ROOM_FAILED", str(error), output, flush=True)
        return 1
    finally:
        subprocess.run(device + ["shell", "am", "force-stop", "com.airwar.together"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        for process in reversed(handles):
            if process.poll() is None:
                process.terminate()
        for process in reversed(handles):
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        for file in files:
            file.close()


if __name__ == "__main__":
    raise SystemExit(main())
