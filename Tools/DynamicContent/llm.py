"""Model backend: runs Claude headlessly through the Claude Code CLI (`claude -p`).

The CLI uses the developer's existing Claude Code login, so no API key is stored
or needed. Each call is a fresh, tool-less session with structured JSON output
(`--json-schema`), which makes the generator and critic independent of each other.
"""
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

DEFAULT_MODEL = "claude-sonnet-5-5"


class LLMError(RuntimeError):
    pass


class ClaudeCliBackend:
    name = "claude-cli"

    def __init__(self, model=DEFAULT_MODEL, timeout=300):
        self.exe = shutil.which("claude")
        if not self.exe:
            raise LLMError("Claude Code CLI ('claude') not found on PATH. Install Claude Code and log in.")
        self.model = model
        self.timeout = timeout
        # Run outside the repo so the session doesn't pull in CLAUDE.md or project settings:
        # the only context each call sees is the prompt this pipeline builds.
        self.cwd = Path(tempfile.gettempdir()) / "rural-rumble-dynamic-content"
        self.cwd.mkdir(exist_ok=True)

    def complete_json(self, role, system, user, schema):
        """Return (parsed JSON object, call metadata). `role` is only recorded."""
        cmd = [self.exe, "-p", "--output-format", "json", "--tools", "", "--no-session-persistence",
               "--model", self.model, "--system-prompt", system, "--json-schema", json.dumps(schema)]
        try:
            proc = subprocess.run(cmd, input=user, capture_output=True, text=True, encoding="utf-8",
                                  timeout=self.timeout, cwd=self.cwd)
        except subprocess.TimeoutExpired:
            raise LLMError(f"{role}: claude -p timed out after {self.timeout}s") from None
        if proc.returncode != 0:
            raise LLMError(f"{role}: claude -p exited {proc.returncode}: {(proc.stderr or proc.stdout)[:500]}")
        try:
            data = json.loads(proc.stdout)
        except json.JSONDecodeError:
            raise LLMError(f"{role}: CLI output was not JSON: {proc.stdout[:500]}") from None
        if data.get("is_error"):
            raise LLMError(f"{role}: CLI reported an error: {data.get('result')}")
        result = data.get("structured_output")
        if result is None:
            try:
                result = json.loads(data.get("result") or "")
            except json.JSONDecodeError:
                raise LLMError(f"{role}: no structured output in CLI result: {str(data.get('result'))[:500]}") from None
        meta = {"backend": self.name, "role": role, "model": self.model,
                "session_id": data.get("session_id"), "cost_usd": data.get("total_cost_usd"),
                "duration_ms": data.get("duration_ms")}
        return result, meta
