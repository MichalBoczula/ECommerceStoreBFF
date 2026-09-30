# Architecture Decision Records

Records describe decisions implemented in this repository. Use sequential numbers and keep accepted records when a later decision supersedes one.

| ADR | Status | Decision |
| --- | --- | --- |
| [0001](0001-yarp-boundary.md) | Accepted | YARP remains the public HTTP boundary; Kiota clients reflect the upstream contracts. |
| [0002](0002-scanned-image-publication.md) | Accepted | Publish the scanned BFF image only after the `master` quality gate. |
| [0003](0003-registration-orchestration.md) | Accepted | Coordinate customer creation and an Invoice cart through an explicit retryable route. |
| [0004](0004-payments-boundary.md) | Accepted | Proxy payment creation and reads without inferring settlement from a created payment. |
