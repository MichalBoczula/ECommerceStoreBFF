"""Publish BFF verification evidence for the pinned Web consumer Pact."""

import argparse
import hashlib
import json
import os
from pathlib import Path

from registry import Registry

ROOT = Path(__file__).resolve().parents[2]
REPOSITORY = "https://github.com/MichalBoczula/ECommerceStoreBFF"
SOURCE_REF = "refs/heads/master"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()

    if os.environ["GITHUB_EVENT_NAME"] != "push" or os.environ["GITHUB_REF"] != SOURCE_REF:
        raise ValueError("Registry publication requires a BFF master push.")

    source = json.loads((ROOT / "contracts/pact/web-source.json").read_bytes())
    report = json.loads(args.report.read_bytes())
    sha = os.environ["GITHUB_SHA"]
    digest = source["contractSha256"]
    if (
        report["consumerCommitSha"] != source["commitSha"]
        or report["contractSha256"] != digest
        or report["providerCommitSha"] != sha
    ):
        raise ValueError("The provider report does not match the pinned Pact and workflow commit.")

    call = Registry().call
    publication_id = source["publicationId"]
    publication = call(f"/api/contract-publications/{publication_id}")
    provenance = {
        "id": publication_id, "kind": "ConsumerPact", "artifactSha256": digest, "commitSha": source["commitSha"],
        "sourceApplicationId": "web", "consumerApplicationId": "web", "providerApplicationId": "bff",
        "sourceRef": SOURCE_REF, "buildUrl": f"https://github.com/{source['repository']}/actions/runs/{source['workflowRunId']}",
    }
    if any(publication.get(key) != value for key, value in provenance.items()):
        raise ValueError("The registry publication does not match the pinned Web artifact.")
    if hashlib.sha256(call(f"/api/artifacts/{digest}/content", raw=True)).hexdigest() != digest:
        raise ValueError("Registry artifact bytes differ from the verified Pact.")

    run = os.environ["GITHUB_RUN_ID"]
    build_url = f"{REPOSITORY}/actions/runs/{run}"
    call("/api/applications/bff/versions", {"commitSha": sha, "repositoryUrl": REPOSITORY})
    receipt = call("/api/verification-runs", {
        **report,
        "consumerPublicationId": publication_id, "consumerApplicationId": "web", "providerApplicationId": "bff",
        "ciRunId": run, "ciAttempt": int(os.environ["GITHUB_RUN_ATTEMPT"]), "ciCheckId": "web-bff-registration",
        "sourceRef": SOURCE_REF, "buildUrl": build_url, "evidenceUrls": [build_url],
    })
    if (
        receipt["contractSha256"] != digest or receipt["status"] != report["status"]
        or receipt["consumerPublicationId"] != publication_id or receipt["providerCommitSha"] != sha
    ):
        raise ValueError("The registry returned a different verification result.")

    args.receipt.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"Published BFF verification {receipt['id']} ({receipt['status']}).")


if __name__ == "__main__":
    main()
