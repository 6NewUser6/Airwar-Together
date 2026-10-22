"""Run real ENet room-capacity checks against a separate authoritative Godot server.

Default: 32 independent headless client processes, then an overflow client.
Use --mode swarm for a cheaper test with 32 independent ENet peers in one client process.
Every mode uses real UDP connections; no players are inserted directly into the server simulation.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import socket
import subprocess
import time


def records(path: Path) -> list[dict]:
    if not path.exists():
        return []
    result = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            result.append(json.loads(line))
        except json.JSONDecodeError:
            pass  # A live writer may be between its write and newline.
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1] / "airwar-together")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "artifacts" / "arena-network")
    parser.add_argument("--mode", choices=("processes", "swarm"), default="processes")
    parser.add_argument("--listen", action="store_true", help="Test 1 local host + 31 network clients instead of dedicated 32 clients")
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--duration", type=int, default=35)
    args = parser.parse_args()
    if not args.port:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as reserve:
            reserve.bind(("127.0.0.1", 0))
            args.port = reserve.getsockname()[1]
    run_dir = args.output.resolve() / time.strftime("%Y%m%d-%H%M%S")
    run_dir.mkdir(parents=True, exist_ok=False)
    processes: list[tuple[str, subprocess.Popen, object]] = []

    def launch(name: str, mode: str, duration: int, index: int = 0) -> subprocess.Popen:
        log = (run_dir / f"{name}.log").open("w", encoding="utf-8")
        command = [str(Path(args.godot).resolve()), "--headless", "--path", str(args.project.resolve()),
                   "--audio-driver", "Dummy", "res://scenes/arena/NetworkCheck.tscn", "--",
                   f"--arena-network-test={mode}", f"--arena-port={args.port}",
                   f"--arena-duration={duration}", f"--arena-index={index}",
                   f"--arena-count={31 if args.listen else 32}",
                   f"--arena-report={run_dir / (name + '.jsonl')}"]
        extra = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, **extra)
        processes.append((name, process, log))
        return process

    def wait_for(predicate, seconds: float, description: str) -> None:
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if predicate():
                return
            if server.poll() is not None:
                raise RuntimeError(f"Server exited before {description}: {server.returncode}")
            time.sleep(.2)
        raise TimeoutError(description)

    try:
        # Startup allowance matters when 32 independent .NET runtimes are started together.
        server_duration = args.duration + (70 if args.mode == "processes" else 10)
        server = launch("server", "listen" if args.listen else "server", server_duration)
        wait_for(lambda: any(r.get("type") == "listening" and r.get("result") == "Ok" for r in records(run_dir / "server.jsonl")),
                 20, "server listening")
        if args.mode == "swarm":
            launch("swarm", "swarm", args.duration)
        else:
            for index in range(31 if args.listen else 32):
                launch(f"client-{index:02}", "client", args.duration + 40, index)
                time.sleep(.1)
            wait_for(lambda: any(r.get("type") == "server" and r.get("players") == 32 for r in records(run_dir / "server.jsonl")),
                     45, "all 32 clients admitted")
            launch("overflow", "overflow", 5, 99)
        print(f"Real ENet {args.mode} run started: {run_dir}", flush=True)
        client_deadline = time.monotonic() + args.duration + 65
        while any(p.poll() is None for name, p, _ in processes if name != "server") and time.monotonic() < client_deadline:
            time.sleep(.5)
        client_results = {}
        for name, process, _ in processes:
            if name == "server":
                continue
            rows = records(run_dir / f"{name}.jsonl")
            final = next((r for r in reversed(rows) if r.get("type") == "result"), {})
            client_results[name] = {"exit": process.poll(), "passed": final.get("passed", False),
                                    "joined": final.get("joined"), "rejected": final.get("rejected"), "reconnected": final.get("reconnected")}
        server_rows = [r for r in records(run_dir / "server.jsonl") if r.get("type") == "server"]
        peak = max((r.get("peak", 0) for r in server_rows), default=0)
        last = server_rows[-1] if server_rows else {}
        server_evidence = peak == 32 and last.get("shots", 0) > 100 and last.get("warps", 0) >= 20
        passed = server_evidence and all(r["passed"] and r["exit"] == 0 for r in client_results.values())
        result = {"passed": passed, "mode": args.mode, "listen": args.listen, "port": args.port, "server_peak": peak,
                  "server_metrics": last, "clients": client_results,
                  "limitations": "Localhost capacity and authority test; not an Android device or WAN impairment test."}
        (run_dir / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if passed else 1
    finally:
        for _, process, _ in processes:
            if process.poll() is None:
                process.terminate()
        for _, process, log in processes:
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            log.close()


if __name__ == "__main__":
    raise SystemExit(main())
