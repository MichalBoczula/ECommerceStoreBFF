# ADR-0002: Publish the scanned BFF image after the quality gate

- Status: Accepted
- Date: 2026-09-29

## Context

The CI pipeline builds, tests and scans the BFF. Docker Hub consumers need an identifiable image corresponding to a successful `master` commit.

## Decision

Build the image only after the mandatory quality gate, smoke-test its `/health`, then scan the local image with Trivy for fixable HIGH/CRITICAL vulnerabilities. Pull requests end after the scan. On a green push to `master`, use `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` secrets to push that same image to `mb0101/ecommerce-store-bff-api` under the full commit SHA and `latest` tags. Verify the local tag image IDs and matching published digests. Do not rebuild between scan and push.

## Consequences

A failed build, test, quality gate, smoke test, scan or push prevents successful publication. The SHA tag identifies a specific build; `latest` moves with successful publications. The workflow distributes an image but does not deploy it to a runtime. Docker Hub access, credential rotation and runtime deployment remain operational concerns.

## Alternatives considered

- Publish from pull requests: distributes unmerged changes.
- Rebuild after scanning: could publish different bytes from those scanned.
