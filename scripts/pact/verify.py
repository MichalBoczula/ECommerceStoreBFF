"""Replay the pinned Web Pact through the running BFF registration API."""

import argparse
import hashlib
import json
import os
import re
import subprocess
import threading
import time
from contextlib import contextmanager
from datetime import UTC, datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from importlib.metadata import version
from pathlib import Path
from urllib.request import urlopen

from pact import Verifier

ROOT = Path(__file__).resolve().parents[2]
HOST = "127.0.0.1"
NEW_CUSTOMER = "the requested customer does not exist"
LEGACY_CUSTOMER = "the requested customer exists without a cart"


def timestamp():
    return datetime.now(UTC).isoformat(timespec="milliseconds")


@contextmanager
def upstreams(fixture):
    class UpstreamHandler(BaseHTTPRequestHandler):
        def read_json(self):
            if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
                # HttpClient can stream PostAsJsonAsync bodies without a Content-Length.
                chunks = []
                while size := int(self.rfile.readline().strip(), 16):
                    chunks.append(self.rfile.read(size))
                    self.rfile.read(2)
                return json.loads(b"".join(chunks))
            return json.loads(self.rfile.read(int(self.headers["Content-Length"])))

        def dispatch(self):
            registration = fixture["registration"]
            customer_id = fixture["customerId"]
            match self.command, self.path:
                case "GET", path if path == f"/customers/external/{registration['externalId']}":
                    if fixture["customer"] is not None:
                        return 200, fixture["customer"]
                    return 404, {"status": 404}
                case "POST", "/customers" if fixture["customer"] is None:
                    if self.read_json() != dict(registration, companies=[]):
                        return 400, {"status": 400}
                    fixture["customer"] = fixture["profile"]
                    return 201, fixture["customer"]
                case "GET", path if path == f"/shopping-carts/client/{customer_id}":
                    if fixture["cart"] is not None:
                        return 200, fixture["cart"]
                    return 404, {"status": 404}
                case "POST", path if path == f"/shopping-carts/{customer_id}" and fixture["customer"] is not None:
                    fixture["cart"] = {
                        "id": "46000000-0000-4000-8000-000000000010",
                        "clientId": customer_id,
                        "lines": [],
                    }
                    return 201, fixture["cart"]
            return 500, {"status": 500, "detail": "Unexpected request to the registration fixture."}

        def respond(self):
            status, value = self.dispatch()
            body = json.dumps(value).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        do_GET = respond
        do_POST = respond

    with ThreadingHTTPServer((HOST, 0), UpstreamHandler) as server:
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://{HOST}:{server.server_port}"
        finally:
            server.shutdown()
            thread.join()


@contextmanager
def bff_api(dll, upstream_url, log_path):
    env = {
        **os.environ,
        "ASPNETCORE_URLS": f"http://{HOST}:0",
        "Logging__LogLevel__Microsoft.Hosting.Lifetime": "Information",
        "Registration__InvoiceBaseUrl": upstream_url,
        **{
            f"ReverseProxy__Clusters__{cluster}__Destinations__destination1__Address": upstream_url
            for cluster in ("products-cluster", "users-cluster", "orders-cluster", "payments-cluster")
        },
    }
    with log_path.open("w", encoding="utf-8") as log:
        process = subprocess.Popen(
            ["dotnet", str(dll)], cwd=dll.parent, env=env, stdout=log, stderr=subprocess.STDOUT,
        )
        try:
            deadline = time.monotonic() + 30
            while process.poll() is None and time.monotonic() < deadline:
                match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", log_path.read_text(encoding="utf-8"))
                if match:
                    base = match[1]
                    with urlopen(f"{base}/health", timeout=5) as response:
                        if response.status != 200:
                            raise RuntimeError("The BFF candidate is unhealthy.")
                    yield base
                    return
                time.sleep(0.1)
            raise RuntimeError(f"The BFF candidate did not start. See {log_path}.")
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


def replay(base_url, contract, fixture, created_carts):
    def set_state(state, action, parameters):
        if state not in (NEW_CUSTOMER, LEGACY_CUSTOMER):
            raise ValueError(f"Unknown BFF Pact state: {state}")
        if action == "teardown":
            created_carts.append(fixture["cart"] is not None)
            fixture.clear()
            return
        registration = parameters["registration"]
        profile = dict(registration, id=parameters["customerId"], companies=[], updatedAt="2026-01-01T00:00:00Z")
        fixture.update(parameters, profile=profile, customer=profile if state == LEGACY_CUSTOMER else None, cart=None)

    verifier = (
        Verifier("bff", host=HOST)
        .add_transport(url=base_url)
        .add_source(contract)
        .state_handler(set_state, teardown=True)
    )
    try:
        verifier.verify()
    except RuntimeError:
        # A failed interaction still yields a report for the registry.
        pass
    return verifier.results


def execution_error(code, message):
    return "Error", 0, 0, [{"kind": "ExecutionError", "code": code, "message": message}]


def normalize(result, interactions, created_carts):
    expected = {item["description"]: item["request"] for item in interactions}
    rows = result.get("interactionResults", [])
    if len(rows) != len(expected) or {row["description"] for row in rows} != expected.keys():
        return execution_error("IncompletePactExecution", "Pact did not report every interaction.")

    passed = failed = 0
    issues = []
    for row in rows:
        if row["pending"]:
            return execution_error("IncompletePactExecution", "Pact reported a pending interaction.")
        if row["result"] == "OK":
            passed += 1
            continue
        name = row["description"]
        mismatches = row.get("mismatch", {}).get("mismatches", [])
        if not mismatches:
            return execution_error("PactExecutionError", "Pact could not execute an interaction. See the CI artifact.")
        failed += 1
        for mismatch in mismatches:
            issue = {
                "kind": "Mismatch", "code": mismatch["type"], "message": mismatch["mismatch"][:4096],
                "interaction": name, "method": expected[name]["method"], "path": expected[name]["path"],
                "jsonPath": mismatch.get("path") or mismatch.get("key") or "$",
                "expected": mismatch.get("expected"), "actualPresent": "actual" in mismatch,
            }
            if issue["actualPresent"]:
                issue["actual"] = mismatch["actual"]
            issues.append(issue)
    if failed:
        return "Failed", passed, failed, issues
    if not result["result"]:
        return execution_error("PactExecutionError", "Pact reported an execution error.")
    if created_carts != [True] * len(interactions):
        return execution_error("CartNotCreated", "BFF did not create a cart for every successful registration interaction.")
    return "Passed", passed, failed, issues


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--provider-dll", type=Path, required=True)
    parser.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    args.results.mkdir(parents=True, exist_ok=True)

    contract = ROOT / "contracts/pact/web-bff.json"
    source = json.loads(contract.with_name("web-source.json").read_bytes())
    content = contract.read_bytes()
    pact = json.loads(content)
    interactions = pact["interactions"]
    if (
        hashlib.sha256(content).hexdigest() != source["contractSha256"]
        or len(interactions) != source["interactionCount"]
        or pact["consumer"]["name"] != "web"
        or pact["provider"]["name"] != "bff"
        or pact["metadata"]["pactSpecification"]["version"] != "4.0"
    ):
        raise ValueError("The pinned Web Pact identity or bytes changed.")

    started = timestamp()
    fixture = {}
    created_carts = []
    with upstreams(fixture) as upstream_url:
        with bff_api(args.provider_dll.resolve(strict=True), upstream_url, args.results / "bff.log") as base:
            result = replay(base, contract, fixture, created_carts)
    write_json(args.results / "pact-results.json", result)
    status, passed, failed, issues = normalize(result, interactions, created_carts)
    configuration = b"\0".join(Path(__file__).with_name(name).read_bytes() for name in ("verify.py", "requirements.txt"))
    write_json(args.results / "verification.json", {
        "consumerCommitSha": source["commitSha"], "contractSha256": source["contractSha256"],
        "providerCommitSha": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "verifierName": "pact-python", "verifierVersion": version("pact-python"),
        "configurationSha256": hashlib.sha256(configuration).hexdigest(),
        "status": status, "verifiedInteractions": passed, "failedInteractions": failed, "issues": issues,
        "summary": f"BFF registration Pact verification {status.lower()} with {passed} matching interactions.",
        "startedAt": started, "finishedAt": timestamp(),
    })
    return 0 if status == "Passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
