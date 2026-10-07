# Privacy, retention and offline boundaries

User profiles retain only email/name/optional phone, Identity security state and necessary operational timestamps. No additional learner demographics are introduced. Memberships, device ownership, GUID audit actors and checkpoints are identifying data even when they contain no names. Treat exports/backups/logs as sensitive and restrict them by purpose and role.

Refresh credentials and device credentials are returned only at issuance/rotation and persisted only as SHA-256 hashes. JWTs and opaque cursors are bearer-like secrets; clients must keep them in OS-protected storage. Do not place them in analytics, crash reports or browser localStorage by default. Sync responses contain global curriculum and published catalogue metadata only, never users, memberships, Identity/security fields or private draft content. Local/microserver catalogues must enforce their own secure network, storage and user access boundaries; these contracts do not authorize redistributing private material.

Retention decisions must be approved before deployment:

| Category | Current behaviour | Required operational decision |
| --- | --- | --- |
| Users, memberships, organisations | Deactivated, retained | Legal purpose, retention and reviewed erasure/anonymisation process |
| Refresh-session hashes | Expiry/revocation enforced; history retained | Retain reused-token detection until family absolute expiry plus incident window; reviewed purge afterwards |
| Device hashes and checkpoints | Revoked/expired proof denied; records retained | Inventory retention, lost-device procedure and reset/rebootstrap policy |
| Audit | Identifier-only append-only, no automatic purge | Access, legal retention and privileged archival/destruction process |
| Sync snapshots/tombstones | Retained; protected cursors expire after configured window | History compaction must preserve replay or require explicit rebootstrap; no independent deletion |
| Jobs and immutable objects | Bounded retries; failure/history retained | Terminal failure review and orphan/retention reconciliation |
| Logs/traces/metrics | Sanitized; sampled traces | Collector permissions, bounded retention and deletion policy |
| Backups/key material | Operator-managed | Encryption, separate access, restore tests and documented expiry |

No automatic retention job destroys audit, refresh-reuse evidence or sync tombstones. Administrative erasure requires a dedicated reviewed workflow accounting for foreign keys, audit obligations, backups and offline copies; deactivation is not erasure. Database-at-rest/object-volume encryption and secret-manager access are deployment responsibilities. API TLS and PostgreSQL certificate validation are mandatory production boundaries.

Offline contract: apply sync items and next cursor atomically in the client's SQLite transaction; use GUID/type as identity, idempotent upsert and tombstone handling. Do not expose archived content or assets after applying its content tombstone. Check SHA-256 over complete downloaded bytes before exposing an asset; use ETag/If-Range when resuming. Resume only with the same authenticated user/device proof; rotate revoked/lost credentials through authorized central administration. The API does not implement Flutter storage or local authentication.

A Microserver is a registered device platform using the same authenticated, read-only sync and download protocol. Its credential is not an organisation-wide API key and does not bypass active user/membership/permission checks. Service-account delegation, local learner identity, federation and offline credential authority require separate product/security design. There are no generic arbitrary upstream writes; learning progress/assessment/attendance conflict policies are intentionally deferred with those domains.
