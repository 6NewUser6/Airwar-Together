"""Read-only integration check against the live Godot editor over MCP HTTP."""

import argparse
import json
import time
import urllib.error
import urllib.request
from pathlib import Path


class McpClient:
    def __init__(self, url):
        self.url = url
        self.session_id = None
        self.protocol = "2025-03-26"
        self.request_id = 0

    def send(self, method, params=None, notification=False):
        payload = {"jsonrpc": "2.0", "method": method, "params": params or {}}
        if not notification:
            self.request_id += 1
            payload["id"] = self.request_id
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            "MCP-Protocol-Version": self.protocol,
        }
        if self.session_id:
            headers["Mcp-Session-Id"] = self.session_id
        request = urllib.request.Request(
            self.url, json.dumps(payload).encode(), headers, method="POST"
        )
        with urllib.request.urlopen(request, timeout=30) as response:
            self.session_id = response.headers.get("Mcp-Session-Id", self.session_id)
            if notification:
                return None
            if "text/event-stream" in response.headers.get("Content-Type", ""):
                result = None
                for line in response:
                    if line.startswith(b"data:"):
                        message = json.loads(line[5:].strip())
                        if message.get("id") == payload["id"]:
                            result = message
                            break
                if result is None:
                    raise RuntimeError("MCP stream ended without a response")
            else:
                result = json.load(response)
        if "error" in result:
            raise RuntimeError(json.dumps(result["error"], ensure_ascii=False))
        return result["result"]

    def close(self):
        if self.session_id:
            request = urllib.request.Request(
                self.url,
                headers={"Mcp-Session-Id": self.session_id, "MCP-Protocol-Version": self.protocol},
                method="DELETE",
            )
            with urllib.request.urlopen(request, timeout=10):
                pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:5300/mcp")
    parser.add_argument("--wait", type=int, default=60)
    args = parser.parse_args()
    client = McpClient(args.url)
    initialized = client.send("initialize", {
        "protocolVersion": client.protocol,
        "capabilities": {},
        "clientInfo": {"name": "airwar-mcp-verification", "version": "1.0.0"},
    })
    client.protocol = initialized["protocolVersion"]
    client.send("notifications/initialized", notification=True)
    try:
        deadline = time.monotonic() + args.wait
        while True:
            tools = client.send("tools/list")["tools"]
            if any(tool["name"] == "editor-application-get-state" for tool in tools):
                break
            if time.monotonic() >= deadline:
                raise RuntimeError("Godot editor tools did not become available")
            time.sleep(1)
        results = {}
        for name in ("editor-application-get-state", "filesystem-list", "scene-list-opened"):
            result = client.send("tools/call", {"name": name, "arguments": {}})
            if result.get("isError"):
                raise RuntimeError(f"{name}: {json.dumps(result, ensure_ascii=False)}")
            results[name] = result
        report = {
            "url": args.url,
            "server": initialized["serverInfo"],
            "protocolVersion": client.protocol,
            "toolCount": len(tools),
            "toolNames": [tool["name"] for tool in tools],
            "checks": results,
        }
        output_path = Path(__file__).resolve().parents[1] / ".tools/run/mcp-verification.json"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({
            "server": report["server"],
            "toolCount": len(tools),
            "passed": list(results),
            "report": str(output_path),
        }, ensure_ascii=False, indent=2))
    finally:
        client.close()


if __name__ == "__main__":
    main()
