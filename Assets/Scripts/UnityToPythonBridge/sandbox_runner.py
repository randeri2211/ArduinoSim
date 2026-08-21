# Host-side (trusted) counterpart to sandbox/worker.py. Owns the sandboxed container's
# lifecycle and brokers its SensorData/DriveMotor requests through to Unity over the
# existing socket in Utils.py -- the container itself never talks to Unity directly,
# it has no network access at all.
import json
import os
import queue
import subprocess
import threading
import time

from UnityToPythonBridge.Utils import _send_command

_IMAGE_NAME = "arduinosim-sandbox"
_CONTAINER_NAME = "arduinosim-sandbox-run"
_SANDBOX_DIR = os.path.join(os.path.dirname(__file__), "sandbox")
_WATCHDOG_TIMEOUT = 30.0  # seconds to wait for a response before treating it as hung

_process = None
_stdout_queue = None
_ready = False


def _run(args, timeout=None):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout)


def docker_available() -> bool:
    try:
        return _run(["docker", "info"], timeout=10).returncode == 0
    except Exception:
        return False


def _build_image_if_missing() -> bool:
    if _run(["docker", "image", "inspect", _IMAGE_NAME]).returncode == 0:
        return True
    print(f"Building sandbox image '{_IMAGE_NAME}'...", flush=True)
    build = _run(["docker", "build", "-t", _IMAGE_NAME, _SANDBOX_DIR])
    if build.returncode != 0:
        print(f"Sandbox image build failed:\n{build.stderr}", flush=True)
        return False
    return True


def _reader_loop(proc, q):
    for line in proc.stdout:
        q.put(line)
    q.put(None)  # pipe closed


def _start_container():
    global _process, _stdout_queue
    # Clean up any stale container left behind by a previous ungraceful shutdown.
    _run(["docker", "rm", "-f", _CONTAINER_NAME])

    args = [
        "docker", "run", "-i", "--rm",
        "--name", _CONTAINER_NAME,
        "--network", "none",
        "--read-only",
        "--tmpfs", "/tmp:rw,size=16m,noexec",
        "--cap-drop", "ALL",
        "--security-opt", "no-new-privileges",
        "--memory", "128m",
        "--cpus", "0.5",
        "--pids-limit", "64",
        _IMAGE_NAME,
    ]
    _process = subprocess.Popen(
        args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, bufsize=1,
    )
    _stdout_queue = queue.Queue()
    threading.Thread(target=_reader_loop, args=(_process, _stdout_queue), daemon=True).start()


def _stop_container():
    global _process
    _run(["docker", "kill", _CONTAINER_NAME])
    if _process is not None:
        try:
            _process.kill()
            _process.wait(timeout=5)
        except Exception:
            pass
        _process = None


def _restart_container():
    print("Sandbox not responding -- restarting it.", flush=True)
    _stop_container()
    _start_container()


def init() -> bool:
    global _ready
    if not docker_available():
        print("Docker is not installed or not running -- code execution is disabled.", flush=True)
        print("Install/start Docker Desktop to enable running code.", flush=True)
        _ready = False
        return False
    if not _build_image_if_missing():
        _ready = False
        return False
    _start_container()
    _ready = True
    return True


def run_code(code: str):
    if not _ready:
        print("Sandbox not available -- code execution is disabled.", flush=True)
        return

    print(code)
    _process.stdin.write(json.dumps({"type": "exec", "code": code}) + "\n")
    _process.stdin.flush()

    deadline = time.time() + _WATCHDOG_TIMEOUT
    while True:
        remaining = deadline - time.time()
        if remaining <= 0:
            _restart_container()
            print("Code execution timed out and was stopped.", flush=True)
            return

        try:
            line = _stdout_queue.get(timeout=remaining)
        except queue.Empty:
            _restart_container()
            print("Code execution timed out and was stopped.", flush=True)
            return

        if line is None:
            _restart_container()
            print("Sandbox exited unexpectedly and was restarted.", flush=True)
            return

        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            continue

        msg_type = msg.get("type")
        if msg_type == "output":
            print(msg.get("text", ""), end="", flush=True)
        elif msg_type == "error":
            print(msg.get("text", ""), flush=True)
        elif msg_type == "request":
            reply_value = _send_command(msg["cmd"], msg["robot"], msg["data"])
            _process.stdin.write(json.dumps({"type": "reply", "value": reply_value}) + "\n")
            _process.stdin.flush()
            deadline = time.time() + _WATCHDOG_TIMEOUT  # still active, don't time out
        elif msg_type == "done":
            return


def shutdown():
    _stop_container()
