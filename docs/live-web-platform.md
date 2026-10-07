# Mwabu Learn live web platform

This branch adds the live React web client to the existing .NET 10 modular monolith. Domain, Application, Infrastructure and API remain the backend boundaries. No new architectural framework, web offline engine or Flutter implementation is introduced.

## Build and run

Requirements: .NET 10, Node 24, pnpm 11.25.0, PostgreSQL, and a trusted HTTPS certificate. Run from the repository root:

```text
dotnet restore backend/MwabuLearn.slnx
dotnet build backend/MwabuLearn.slnx --configuration Release --no-restore -warnaserror
dotnet test backend/MwabuLearn.slnx --configuration Release --no-build --no-restore
cd web
pnpm install --frozen-lockfile
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

The simplest local integration serves the built web application through the API. Set the existing backend configuration using environment variables or User Secrets, then run the API with `--webroot` pointing to the absolute `web/dist` directory. For example, from `backend/src/MwabuLearn.Api`, `dotnet run --configuration Release --no-build --no-launch-profile -- --webroot C:/Development/mwabu-learn-v2/web/dist`. Configure HTTPS through standard Kestrel settings. Never commit credentials, certificates or local appsettings.

For Vite hot reload, supply `MWABU_WEB_CERT` and `MWABU_WEB_KEY` as paths to a locally trusted PEM certificate/key, and `MWABU_API_ORIGIN` as the HTTPS API origin. Vite proxies `/api` with certificate verification enabled. Where Node requires an additional local CA, set `NODE_EXTRA_CA_CERTS` to that CA certificate before starting Node. Do not set `NODE_TLS_REJECT_UNAUTHORIZED=0`. `pnpm dev` requires both certificate paths; HTTP preview is unsuitable for Secure authentication cookies.

All browser API requests are deliberately same-origin `/api` requests. There is no public API secret or token in Vite configuration. Deploying API and UI to unrelated origins would require a separate reviewed cookie/CORS/CSRF design; changing a build-time URL alone is not sufficient.

## Browser authentication

The browser transport reuses existing Identity, JWT validation, refresh families and strict replay detection. Login sets `__Host-MwabuAccess` and `__Host-MwabuRefresh`: Secure, HttpOnly, SameSite Strict, host-only, root path. JavaScript receives expiry metadata, never either credential. Nothing is persisted in localStorage, sessionStorage or IndexedDB.

`GET /api/browser/session/csrf` issues an ASP.NET antiforgery request token. Every unsafe browser session request, including anonymous login, requires `X-Mwabu-CSRF`. Cookie-bearing writes to legacy API routes require the same protection. Bearer-only mobile/API clients retain their contracts. A supplied invalid Authorization header never falls back to browser cookies.

`GET /api/browser/session` reports validity, server time and expiry. Browser rotations use an in-tab single-flight promise and the origin-wide Web Locks API; the lock is shared with login/logout. Each waiter checks the current shared cookie before rotating. A timer renews shortly before access expiry. A 401 can trigger one coordinated renewal; 409 refresh conflicts and lost responses are not automatically replayed. Browsers without Web Locks fail closed when renewal is necessary and require a new login. BroadcastChannel sends credential-free session-change notifications, clearing private query data in other tabs. Logout revokes the family even when the access cookie has expired. Logout-all and password changes use the existing server revocation rules.

Recovery screens call the existing recovery adapter. Its default unavailable response is shown honestly; this feature does not simulate sending mail, expose recovery codes in URLs or introduce an email infrastructure.

## Permissions and organisation context

`GET /api/workspace/access?organisationId=...` returns effective permissions for rendering controls. This DTO is not an authorization credential. Every protected server operation independently checks the active account, active organisation, active membership and required stable permission code.

The browser's selected organisation is held in memory. Routes include explicit organisation IDs. Switching organisations cancels requests and removes scoped cached data. Permissions, rather than role labels, determine controls. Role codes only personalise dashboard language. Membership in a parent organisation does not automatically grant access to a school or child organisation.

Global catalogue data remains independent from organisations. Global curriculum/content management now requires the corresponding permission through an active **Platform-type organisation membership**. A ContentManager in the platform organisation can author/publish without platform-wide user administration. A ContentManager or OrganisationAdmin in an ordinary school cannot change the global catalogue. PlatformAdmin authority remains centrally evaluated and is not inferred from a role-name comparison in controllers. Ordinary catalogue reads may use a valid active membership carrying the read permission.

Published catalogue endpoints always enforce Published status. Existing content GET routes also enforce this boundary for callers lacking global content management; callers cannot bypass it through draft IDs, slugs, asset or association routes. Authoring users retain draft visibility. Published/archived immutability and separate publish permission remain enforced by the existing content service. Revisions create new resources; editors must explicitly attach revised files and connections.

Organisation administrators can create a **new account and membership atomically** with both `users.manage` and `memberships.manage` in that organisation. They cannot browse all users, discover existing accounts or attach arbitrary existing accounts. Existing-account membership changes require platform administration. Initial passwords are validated by Identity and never returned or logged.

## Education relationships and privacy

Projects have an owning organisation, lifecycle, dates, optional curriculum version, explicit sites, membership participants and published content assignments. Project permissions are organisation-scoped. Assigning a site requires management permission in that site. Assigning a participant requires membership management in the participant's organisation; project ownership alone does not grant it. Each assignment category supports up to 500 entries, enforced on writes and reads. Removal preserves users, organisations and memberships.

School context reuses Organisation of type School. Assigned curriculum versions are explicit relationships. Guardian links connect separate guardian and learner memberships in the same organisation; composite foreign keys enforce that boundary. Application rules also require the appropriate active roles and accounts. Guardian reads verify the link on every request and expose only the linked learner's name, school and assigned curriculum context. There is no simulated attainment or progress data.

Recent resources are personal convenience records, limited to 100 entries per membership and pruned to 90 days on subsequent visits. They are not achievement or engagement analytics. Expired records for dormant users are not periodically purged; operators should include them in their future data-retention policy.

Reporting contains actual operational counts. Organisation reports distinguish their scope from the shared published catalogue. These are sequential aggregate queries, not an atomic historical snapshot. Device support exposes registration state and bounded acknowledged checkpoint metadata. It never exposes device proof credentials, bearer tokens, raw cursors or storage keys. Credential rotation remains a paired-device/API provisioning operation; the browser does not display a new device secret without a secure transfer workflow.

## Files and media

Private content remains in the configured local/S3 storage adapter outside wwwroot. DTOs omit storage keys. Learning asset endpoints authenticate every request, verify Published status and support range requests. Inline MIME allow-list permits PDFs, common raster images, audio and video; HTML, SVG and packages are download-only. Inline responses have restrictive CSP and frame restrictions. Native media controls provide play/pause/seek/volume; audio/video never autoplay. A resource supports up to 100 files. Upload progress, cancellation, configured size checks and server errors are shown; ambiguous failed uploads require checking the file list before retrying.

## API additions

| Area | Routes |
| --- | --- |
| Browser session | `GET csrf`, `POST login`, `GET /`, `POST refresh`, `POST logout`, `POST logout-all` under `/api/browser/session` |
| Workspace | `GET /api/workspace/access`, `GET /api/workspace/settings` |
| Learning | `GET /api/learning/content`, `GET /api/learning/content/{id}`, `GET .../{id}/assets/{assetId}/view`, `GET .../download` |
| Scoped provisioning | `POST /api/organisations/{organisationId}/users` |
| Projects | `GET/POST /api/organisations/{organisationId}/projects`, `GET/PUT .../{id}`, `POST .../{id}/{sites\|participants\|resources}`, `DELETE .../{id}/{kind}/{assignmentId}` |
| School | Under `/api/organisations/{organisationId}/workspace`: `GET members`, `GET/POST curricula`, `PATCH curricula/{id}/active`, `GET/POST guardian-links`, `PATCH guardian-links/{id}/active`, `GET recent`, `POST recent/{contentId}` |
| Guardians | `GET /api/guardians/me/learners`, `GET .../{linkId}/curricula` |
| Reports | `GET /api/reports/platform`, `GET /api/reports/organisations/{organisationId}`, `GET .../{organisationId}/checkpoints` |
| Administration | `GET /api/users/{id}/memberships` |
| Development | `POST /api/development/seed` |

Existing curriculum, content, directory, session, device and sync routes remain available. Directory queries now support bounded text/parent filtering; published resource search supports every curriculum level, tags and collections. Authentication failures use generic errors and existing sanitized ProblemDetails/correlation IDs.

## Explicit development demonstration seed

Only the Development environment exposes the seed operation. Set `DevelopmentSeed__Enabled=true` explicitly, and separately supply eight distinct strong passwords using `DevelopmentSeed__Passwords__OrganisationAdmin`, `ProjectManager`, `HeadTeacher`, `Teacher`, `Learner`, `ParentGuardian`, `ContentManager`, `DataAnalyst`. Use configuration/User Secrets, never committed settings. Passwords must meet the bootstrap strong-credential checks (16–128 characters, mixed case, digit, symbol, no default-password markers).

First establish a platform administrator through the existing explicitly enabled bootstrap mechanism in Development, supplying its email/password/name/platform organisation configuration. This branch rejects bootstrap enabled in Production. Sign in as that administrator and call the seed endpoint explicitly. Disabled or non-Development seeding returns 404. There is no default universal administrator or demo password.

Seeding creates `DEMO-PROGRAMME`, `DEMO-SCHOOL`, eight `demo.{rolecode-lowercase}@mwabu.invalid` accounts, memberships/roles, a clearly labelled non-official demonstration curriculum, original CC0 PDF lesson and lesson-plan resources, a guardian link and a project. It is idempotent and does not reset existing account passwords. The response lists emails and resource-context IDs, never credentials. Disable both seed and bootstrap after use. Production databases must not receive demonstration accounts.

## Migration and deployment

New additive migration: `20261007174226_LiveWebEducation`. It adds education tables, a membership alternate key supporting same-organisation guardian foreign keys, two project permissions and seven role mappings. Existing ten migrations are preserved. No users/passwords are seeded by migrations. The migration has been applied only to newly created disposable test databases; application and production databases require human review and manual application. Review locking implications of the membership alternate-key index before applying to a large database.

The Dockerfile builds the web app in a Node stage and copies hashed assets to API wwwroot. The existing non-root .NET runtime, private storage directories and health behaviour remain. Use a known trusted HTTPS reverse proxy and existing forwarded-header configuration; do not trust arbitrary forwarded headers. Production database certificate verification and encrypted durable Data Protection keys remain required. Review existing backend/deployment documentation for their environment settings, backups and readiness prerequisites.

SPA fallback is limited to web routes, preserving API 404/405 responses. Index responses are no-store; hashed assets are immutable-cacheable; API responses are no-store. CSP allows same-origin scripts, styles, connections and media, prohibits objects and cross-origin framing, and requires no external font service. Deploy UI/API together and retain previous hashed assets during rolling releases if supporting already-open tabs.

## Verification

Frontend unit/component tests run with Vitest. Playwright uses Chromium against the actual built UI and actual HTTPS API, without mocked API routes. `MWABU_POSTGRES_TEST_SERVER` must identify a loopback maintenance database named postgres on an isolated test cluster. The harness creates only `mwabu_e2e_{random}` databases, applies migrations there, generates temporary certificates and credentials, explicitly seeds data and drops its database on normal shutdown. It never accepts an application database. Do not run it against production. On forcibly terminated runs, inspect and remove only the matching disposable test database and temporary test directory.

```text
cd web
pnpm exec playwright install chromium
pnpm test:e2e
```

Test credentials live only in ignored `.local/e2e/fixture.json` and the temporary process environment. Tracing is disabled because browser network traces can contain login bodies. Certificate-error bypass is restricted to Playwright's disposable test certificate; the application and Vite proxy retain TLS verification. GitHub uses PostgreSQL 17; local verification uses installed PostgreSQL 18. CI retains migration preservation, warnings-as-errors, zero skipped backend tests, model drift, idempotent SQL, self-contained linux-x64 bundle, advisory gates, Linux Docker build and production non-root smoke verification, and adds frontend lint/type checks/tests/build/advisories/real-browser workflows.

The live application is not declared production-ready until the whole workflow succeeds and deployment configuration, security and migration reviews are accepted. Assessment/progress systems, offline Flutter sync UX, email delivery, MFA/SSO, payments, AI and full audit analytics are outside this implementation.
