#!/usr/bin/env python3
"""Run the opt-in DataAccess stress scenario against an isolated, disposable PostgreSQL container."""

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import uuid

POSTGRES_IMAGE = "postgres:16-alpine@sha256:cf78e76683b9ca8c5733cbbdce6c9262b45b6767934dd0a95e671f9a0fc20685"
REPO = Path(__file__).resolve().parents[1]
PROJECT = "tests/Moongate.Persistence.Tests/Moongate.Persistence.Tests.csproj"


def bounded_integer(minimum, maximum):
    def parse(value):
        number = int(value)
        if not minimum <= number <= maximum:
            raise argparse.ArgumentTypeError(f"expected {minimum}..{maximum}")
        return number
    return parse


def monitor(name, stop, path):
    with path.open("w") as output:
        while not stop.is_set():
            try:
                result = subprocess.run(
                    ["docker", "stats", "--no-stream", "--format", "{{json .}}", name],
                    capture_output=True, text=True, timeout=10, check=True,
                )
                sample = json.loads(result.stdout)
                sample["RecordedAtUtc"] = datetime.now(timezone.utc).isoformat()
                output.write(json.dumps(sample) + "\n")
                output.flush()
            except (subprocess.SubprocessError, ValueError) as error:
                output.write(json.dumps({"SamplingError": type(error).__name__}) + "\n")
                output.flush()
            stop.wait(1)


def run_test(command, env, log, timeout):
    # Own the entire process group so interruption/timeout does not leave a test host behind.
    with log.open("w") as output:
        child = subprocess.Popen(command, cwd=REPO, env=env, stdout=output,
                                 stderr=subprocess.STDOUT, start_new_session=True)
        try:
            return child.wait(timeout=timeout)
        finally:
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()


def terminate(signum, _frame):
    raise SystemExit(128 + signum)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sessions", nargs="+", type=bounded_integer(1, 10_000), default=[100, 500, 1000])
    parser.add_argument("--seconds", type=bounded_integer(1, 600), default=30, help="Measured seconds per phase (default: 30)")
    parser.add_argument("--concurrency", type=bounded_integer(1, 256), default=32, help="Maximum in-flight logical operations")
    parser.add_argument("--think-ms", type=bounded_integer(0, 60_000), default=10, help="Delay per session after each operation")
    parser.add_argument("--output-dir", type=Path, help="New directory for results; existing directories are refused")
    args = parser.parse_args()
    if sys.platform != "linux":
        parser.error("This isolated runner requires Linux (WSL2 is also supported).")
    for tool in ("dotnet", "docker"):
        if not shutil.which(tool):
            parser.error(f"{tool} is required on PATH")

    signal.signal(signal.SIGTERM, terminate)
    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
    output = (args.output_dir or REPO / "TestResults" / "persistence-stress" / run_id).resolve()
    output.mkdir(parents=True, exist_ok=False)
    print(f"Results: {output}", flush=True)
    with (output / "build.log").open("w") as log:
        subprocess.run(["dotnet", "build", PROJECT, "-c", "Release", "--nologo"], cwd=REPO,
                       stdout=log, stderr=subprocess.STDOUT, check=True, timeout=600)

    name = "moongate-stress-" + uuid.uuid4().hex
    stop = threading.Event()
    sampler = None
    with tempfile.TemporaryDirectory(prefix="moongate-stress-") as temporary:
        socket = Path(temporary) / "socket"
        socket.mkdir(mode=0o777)
        socket.chmod(0o777)
        try:
            # No ports, network, user database, or credentials. Default PostgreSQL durability stays enabled.
            # The image's anonymous data volume is disk-backed and removed with this container only.
            subprocess.run([
                "docker", "run", "-d", "--name", name, "--network", "none", "--cpus", "2", "--memory", "1g",
                "--mount", f"type=bind,source={socket},target=/var/run/postgresql",
                "-e", "POSTGRES_HOST_AUTH_METHOD=trust", POSTGRES_IMAGE,
                "-c", "listen_addresses=", "-c", "max_connections=300",
            ], check=True, stdout=subprocess.DEVNULL, timeout=120)
            for _ in range(120):
                logs = subprocess.run(["docker", "logs", name], capture_output=True, text=True, timeout=10)
                ready = subprocess.run(["docker", "exec", name, "pg_isready", "-U", "postgres"],
                                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
                if "PostgreSQL init process complete" in logs.stdout and ready.returncode == 0:
                    break
                stop.wait(0.5)
            else:
                raise RuntimeError("Disposable PostgreSQL did not become ready")

            metadata = {
                "PostgresImage": POSTGRES_IMAGE, "PostgresCpuLimit": 2, "PostgresMemoryLimit": "1g",
                "Storage": "Docker anonymous volume; fsync and synchronous_commit use PostgreSQL defaults",
                "Sessions": args.sessions, "Seconds": args.seconds, "Concurrency": args.concurrency,
                "ThinkMilliseconds": args.think_ms,
                "Commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
                "WorkingTreeDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=REPO, text=True)),
            }
            (output / "environment.json").write_text(json.dumps(metadata, indent=2) + "\n")
            sampler = threading.Thread(target=monitor, args=(name, stop, output / "postgres-stats.jsonl"), daemon=True)
            sampler.start()
            env = os.environ.copy()
            env.update({
                "MOONGATE_TEST_POSTGRES_CONNECTION_STRING": f"Host={socket};Username=postgres;Database=postgres;Pooling=false",
                "MOONGATE_RUN_PERSISTENCE_STRESS": "1",
                "MOONGATE_STRESS_SESSIONS": ",".join(map(str, args.sessions)),
                "MOONGATE_STRESS_SECONDS": str(args.seconds),
                "MOONGATE_STRESS_CONCURRENCY": str(args.concurrency),
                "MOONGATE_STRESS_THINK_MS": str(args.think_ms),
                "MOONGATE_STRESS_REPORT": str(output / "report.json"),
            })
            print(f"Running {args.sessions} sessions, {args.seconds}s/phase, concurrency {args.concurrency}...", flush=True)
            code = run_test([
                "dotnet", "test", PROJECT, "-c", "Release", "--no-build", "--nologo",
                "--filter", "FullyQualifiedName~PersistenceStressTests", "--logger", "console;verbosity=normal",
            ], env, output / "test.log", timeout=300 + len(args.sessions) * (args.seconds + 180 + args.think_ms / 1000))
            report = output / "report.json"
            if report.exists():
                for phase in json.loads(report.read_text())["Phases"]:
                    print(f'{phase["Sessions"]} sessions: {phase["SuccessfulOperationsPerSecond"]:.0f} ops/s, '
                          f'verified={phase["Verified"]}, errors={phase["Errors"]}')
            else:
                print("No workload report was written; inspect test.log.", file=sys.stderr)
                code = code or 1
            if code:
                print((output / "test.log").read_text()[-8000:], file=sys.stderr)
            print(f"Results: {output}", flush=True)
            return code
        finally:
            stop.set()
            if sampler:
                sampler.join(timeout=15)
            try:
                subprocess.run(["docker", "exec", "--user", "0", name, "chmod", "0777", "/var/run/postgresql"],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
            finally:
                subprocess.run(["docker", "rm", "-fv", name], stdout=subprocess.DEVNULL,
                               stderr=subprocess.DEVNULL, timeout=30)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("Stress run interrupted; disposable resources cleaned up.", file=sys.stderr)
        sys.exit(130)
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"Stress run failed: {error}", file=sys.stderr)
        sys.exit(1)
