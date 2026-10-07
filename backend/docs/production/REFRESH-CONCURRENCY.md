# Concurrent refresh rotation — PostgreSQL correction

Branch: feature/production-backend. No merge. Original failure: GitHub Actions run [37551463333](https://github.com/Yenga-ui/mwabu-learn-v2/actions/runs/37551463333), 199 passed / 1 failed / 0 skipped. The failing refresh test was retained and strengthened.

## Root cause and evidence

A deterministic test-only SaveChanges rendezvous made both real PostgreSQL transactions read the same original session before either write. Before the production correction it reproduced HTTP 500 and captured this exact exception chain (types/SQLSTATE only, no sensitive messages):

`InvalidOperationException -> DbUpdateException -> PostgresException SQLSTATE=40001`

PostgreSQL rejects the losing SERIALIZABLE update with serialization_failure. Npgsql's non-retrying EF execution strategy adds an InvalidOperationException around the DbUpdateException. IdentityValidation.Postgres previously examined only the outer exception and its first InnerException, so its existing serialization-conflict catch filter missed the PostgreSQL exception two levels down. The generic server handler consequently emitted 500. This was not a duplicate token-hash violation or a missing concurrency token.

The provider wrapper is also confirmed in [NpgsqlExecutionStrategy source](https://raw.githubusercontent.com/npgsql/efcore.pg/v10.0.0/src/EFCore.PG/Storage/Internal/NpgsqlExecutionStrategy.cs). No provider exception messages are returned through the API.

## Mechanism and security semantics

The production correction only walks known InvalidOperationException/DbUpdateException wrappers to find PostgresException. Existing exact SQLSTATE matching remains: 40001 and the previously supported 40P01 map to IdentityError.Conflict / HTTP 409. DbUpdateConcurrencyException handling remains intact. Unexpected internal/connection failures are not translated, swallowed or retried; tests verify they escape unchanged to the server-error path.

The existing SERIALIZABLE transaction, UpdatedAt optimistic concurrency token, unique token-hash index and restrictive replacement relationship are unchanged. PostgreSQL arbitrates concurrent writes across API instances. There are no process locks, caches, retries, additional session inserts or schema changes.

- Winner: commits one replacement, consumes the original, returns 200.
- Overlapping loser that read the same original: transaction aborts/rolls back its insertion and returns 409; it does not revoke the winner.
- A later request that observes an already rotated/revoked original: existing strict reuse detection returns 401 and commits revocation of the entire family, including the winner. A request scheduled after the first commit can intentionally take this replay path even if its HTTP call began concurrently. There is no grace window or blind rotation retry.
- Explicit subsequent original-token replay still revokes the family. Both successor refresh and successor access token then fail.

## Tests and verification

The existing PostgreSQL HTTP race test now forces genuine overlapping writes, excludes accidental bearer challenges from refresh requests, records safe exception-chain diagnostics, verifies exactly one successful response and one committed active replacement, checks the winner remains usable before replay, then proves replay revokes every family row and rejects both access/refresh successor credentials. Allowed concurrency statuses remain 200/401/409; 500 is never accepted.

Added real PostgreSQL sequential rotation/logout/logout-all/password-change invalidation coverage. Four provider-wrapper fault cases verify expected serialization/deadlock translation, unexpected internal/connection failure propagation, rollback and subsequent successful rotation. Existing SQLite/session/Curriculum/Content tests remain included.

Local environment: installed PostgreSQL 18 binaries used to create a new disposable cluster under a uniquely named temporary directory, bound to 127.0.0.1:55439 with random SCRAM credentials. Fixtures migrated only new mwabu_test_GUID databases and dropped those databases. The developer's normal database was never accessed or migrated. No Docker was required.

PostgreSQL 18 additionally exposed an existing restrictive-delete test's version-specific SQLSTATE assertion: ON DELETE RESTRICT is 23001 in 18 and 23503 in 17. Its assertion now selects the precise expected code by server major version and additionally verifies parent/membership rows remain. CI continues using PostgreSQL 17. This is a test portability correction, not a relaxation of restrictive deletion. [Upstream PostgreSQL change](https://github.com/postgres/postgres/commit/086c84b23).

Commands executed:

- dotnet restore backend/MwabuLearn.slnx — success.
- dotnet build backend/MwabuLearn.slnx --no-restore -warnaserror — success, 0 warnings/errors.
- dotnet test backend/MwabuLearn.slnx --no-build --no-restore with disposable PostgreSQL enabled — **205 passed, 0 failed, 0 skipped**.
- Forced PostgreSQL race repeated independently **10 times**, with fresh fixture databases; results recorded in final handover. Additional focused/full-suite race executions also passed after the correction.

No migration was required; no existing migration or snapshot was changed. Final commit SHA, remote-head confirmation and CI rerun status are reported in the handover.

## Files

- Infrastructure/Identity/IdentityValidation.cs — unwrap known provider/EF exception chain.
- Tests/IdentityTestEnvironment.cs — optional test-only interceptor injection.
- Tests/PostgreSqlTests.cs — strengthened retained race, lifecycle coverage, exact PostgreSQL-version RESTRICT assertion.
- Tests/RefreshRaceInterceptor.cs — test-only rendezvous and sanitized exception diagnostics.
- Tests/TransactionFailureTests.cs — precise SQLSTATE translation/rollback/propagation coverage.
- docs/production/REFRESH-CONCURRENCY.md — this report.
- docs/production/PROGRESS.md — verification update.

All source paths are under backend/src or backend/tests as appropriate. User-owned Claude outputs remains untouched and untracked. Main is unchanged.
