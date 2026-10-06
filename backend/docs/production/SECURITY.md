# Authentication and request security

Access JWTs remain short lived and contain subject, version and session family IDs, never role graphs. The server validates current account state and active session family on each request. JWTs issued before this feature without a family claim expire naturally within their configured short lifetime; new logins always issue family-bound tokens.

Login returns a refresh credential once, with idle/absolute expiry and UTC server time. Store it in an OS-protected client credential store. Only a SHA-256 digest of 64 cryptographically random bytes is persisted. POST `/api/auth/refresh` rotates it atomically. Reuse of any previous credential revokes the entire family, including its access tokens. Lost rotation responses require a new login; clients must serialize refresh operations. POST `/api/auth/logout` revokes the supplied caller-owned family; `/logout-all` invalidates every family and increments account token version. POST `/change-password` uses Identity validation and invalidates all sessions.

`Sessions:IdleDays` defaults to 45, `AbsoluteDays` to 180 and `PasswordResetMinutes` to 30. Review these lifetimes for shared school devices. Password reset is implemented with Identity token providers and an explicit `IAccountNotificationService`. No delivery provider is configured by default: forgot/reset routes return 503 for every account. A production adapter must durably accept delivery without distinguishing known accounts through transport failures. No reset token is returned by the HTTP API. Email verification, MFA, session listing and email delivery remain separate integrations.

Set `HttpSecurity:AllowedOrigins` to explicit HTTPS origins (no trailing slash, wildcard, credentials or paths). Production refuses to start with an empty/unsafe list. Development can explicitly allow HTTP origins; an empty nonproduction list permits no cross-origin browser access. Native mobile clients do not use CORS. Credentials/cookies are not enabled.

Forwarded headers default to disabled. At a TLS-terminating proxy set `HttpSecurity:ForwardedHeadersEnabled=true`, exact `KnownProxies` addresses and a bounded `ForwardLimit` (default 1). Only trusted proxies may set scheme/client IP. Do not expose the internal HTTP port publicly. `/health/live` and `/health/ready` may use HTTP inside the private deployment network; API requests retain HTTPS requirements. HSTS is enabled outside Development. Readiness currently validates DB connectivity/schema; storage readiness is still pending.

Authentication bodies are bounded to 16 KiB. Auth attempts are partitioned by client IP and a one-way normalized account/refresh identifier, with a separate generous IP ceiling for shared NATs. `Authentication:LoginAttemptsPerMinute` controls each partition. API requests also have a per-user limit. Additional endpoint-specific configurable upload/sync/search limits remain pending.

Responses include nosniff, DENY frame policy, no-referrer and bounded safe correlation IDs. All auth responses are no-store. JSON request-completion logs contain route templates, status and duration, never body/header/query values. Exception responses use generic ProblemDetails with trace/correlation IDs. `MwabuLearn.Api` ActivitySource/Meter emits bounded route/status duration measurements; an operator exporter integration remains pending.

# Audit

Business SaveChanges produces identifier-only append-only audit rows in the same database transaction. No snapshots, names, email, credential or personal field values are recorded. Critical login/session/password events use fixed codes. An application guard rejects audit updates/deletes; the PostgreSQL migration adds a database trigger as additional protection. Bulk updates must explicitly record their security event (session revocation does so). Future bulk business operations must do the same.

GET `/api/audit-events` requires platform `users.read`; ordinary organisation administrators cannot read platform audit history. Search uses UTC From/To, maximum 90 days (default 7), actor/organisation/entity/event filters, bounded page size 100. Returned identifiers are still sensitive operational data. Set an organisation-approved retention/access policy before deployment; no destructive retention job is enabled. The migration owner's ability to alter tables/triggers is intentionally separate from runtime access; deploy with a least-privilege runtime role.

# Migration safety

No automatic database migration occurs at startup. Set `MWABU_MIGRATIONS_CONNECTION` explicitly for operator database operations. Scaffolding and SQL inspection can use `dotnet ef ... -- --offline`; offline mode refuses every database connection. Take and test PostgreSQL backups before manually applying reviewed migrations. Previous migrations are preserved. Docker/PostgreSQL execution and trigger verification remain unverified locally until isolated infrastructure is available.
