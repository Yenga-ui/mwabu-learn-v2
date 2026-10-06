# Storage, cleanup and devices

`ContentStorage:Provider` is `Local` (default) or `S3`. S3 uses AWSSDK.S3 pinned in the Infrastructure project and the SDK credential provider chain (AWS environment variables, workload identity or managed roles). Never commit access keys. Configure `ContentStorage:S3:Bucket`, `Region`, optional HTTPS `ServiceUrl`, `ForcePathStyle` for compatible providers, and a private `SpoolDirectory`. Existing deterministic object keys are unchanged. No bucket is created automatically.

Uploads count observed bytes and compute SHA-256 while streaming to a private disk spool. Empty/oversized uploads are rejected before any object write. Single conditional PutObject (`If-None-Match: *`) prevents overwrite and exposes no partial objects. The spool is deleted on success/failure/cancellation. This implementation supports the existing maximum 1 GiB upload policy, below S3 single-put limits. Do not enable SDK write retries blindly: ambiguous network failure can leave a complete unreferenced object. Reconciliation of such orphans remains an operator task; automatic destructive bucket sweeps are not enabled. Keep the spool volume private, bounded and monitored; interrupted process leftovers should be reviewed under a deployment retention policy.

Use a private bucket. Grant only required GetObject/PutObject/DeleteObject on the content prefix plus ListBucket for the bounded readiness probe; enforce transport encryption at the provider. A compatible provider must implement conditional writes, checksums and ranged reads correctly. These behaviours are covered against a controlled SDK adapter locally; actual S3/MinIO execution remains pending. Provider documentation: https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TPutObjectRequest.html

Local file downloads and S3 remote streams support HTTP ranges without loading whole media into memory. Strong ETags use the stored SHA-256, never S3 multipart ETags. ASP.NET handles If-Range and unsatisfiable ranges. Provider failures are not exposed in HTTP responses. Storage readiness verifies local write access or bounded S3 listing without returning keys/object names.

Pending asset deletion creates a durable job in the same transaction as the metadata change. Existing synchronous removal still attempts immediate cleanup; the durable job safely completes when it finds the asset already absent. Workers atomically claim bounded batches using lease IDs, perform idempotent deletion, then commit metadata cleanup. Lease ownership is an optimistic concurrency token. Shutdown/crash leaves a recoverable lease; expired leases can be claimed again. Failed attempts use capped exponential delay; exhausted attempts remain `failed` for operator investigation. Error metadata stores fixed codes only. Retention/requeue UI and unrelated processors are not implemented.

`BackgroundWork` options: Enabled=true, PollSeconds=10, BatchSize=20, LeaseSeconds=120, MaximumAttempts=8. Use a lease longer than expected provider deletion time. Runtime credentials need background-job table write access. Multiple workers can race safely on atomic claim predicates; actual concurrent PostgreSQL execution still requires isolated integration verification.

Devices are scoped to an organisation and identified by a server-generated GUID. ClientRegistrationId is a random installation/request GUID, not a hardware identifier. Repeating registration returns the same device without replaying its raw credential; recover a lost response through the authenticated credential rotation route. Credentials are 64 random bytes, stored only as SHA-256 and returned once with no-store. `Devices:CredentialDays` defaults to 180 (review for shared devices).

Registration/get requires organisation `content.read`; listing requires `memberships.manage`. Owners can rotate/revoke their own device; organisation membership managers can manage others. Existing reusable policies validate organisation authority. Revocation is permanent; no record is hard-deleted. Register a new installation after revocation.

GET `/api/devices/current` requires a valid user bearer token AND `X-Device-Id` plus `X-Device-Credential`. Both headers are credentials/context, never logged. The proof is validated against active device, unexpired credential, active user, active organisation, active membership and `content.read`. Platform authority does not bypass the explicit active membership requirement for device use. Optional `X-Organisation-Id` must match the validated device. The same policy will protect sync. LastSeenAt updates are throttled to 15 minutes and no IP/UA/hardware history is stored.

Device routes:
- POST/GET `/api/organisations/{organisationId}/devices`
- GET `/api/organisations/{organisationId}/devices/{id}`
- POST `/api/organisations/{organisationId}/devices/{id}/credential`
- POST `/api/organisations/{organisationId}/devices/{id}/revoke`
- GET `/api/devices/current`
