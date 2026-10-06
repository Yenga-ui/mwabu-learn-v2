# Identity, Roles & Organisations

## Delivery

Branch: `feature/identity-organisations`. Base: latest `main`, commit `484d15f`.
Identity uses standard ASP.NET Core Identity on EF Core/PostgreSQL. Domain entities contain the organisation business model; Application defines DTOs and interfaces; Infrastructure implements persistence, authentication and business services; API owns HTTP context and authorization policies. Controllers never query the database. Existing Curriculum and Content entities, storage, routes and migrations are preserved.

Migration: `20261006200525_IdentityOrganisations`. It has been generated and reviewed, **not applied to PostgreSQL**. Review and apply manually before enabling bootstrap. No merge into main is part of this delivery.

## Identity and data model

`ApplicationUser : IdentityUser<Guid>` lives in Infrastructure so Domain has no dependency on Identity. It adds FirstName, LastName, IsActive, LastLoginAt, UTC CreatedAt/UpdatedAt and an internal AccessTokenVersion. Email is trimmed then normalized by Identity. UserName is the email. Identity owns password hashes, security/concurrency stamps, lockout counters and future token-provider capabilities. No business Role field exists on the user. DTOs contain only the supported profile fields; hashes, stamps, token versions and internal Identity fields are excluded.

`MwabuDbContext : IdentityUserContext<ApplicationUser, Guid>` uses Identity's standard user, claim, external-login and user-token tables. Infrastructure Identity roles are deliberately not enabled: organisation business roles use separate tables. The migration retains standard Identity primary keys and unique UserNameIndex, makes NormalizedEmail/Email required and EmailIndex unique, limits names/email/phone fields and restricts Identity child deletion. Usernames/emails have length 256; names 100; optional phone 30. Standard Identity login/token composite keys are supplied by Identity rather than reimplemented manually.

Organisations have GUID IDs, trimmed Name, uppercase stable Code, string OrganisationType, nullable ParentOrganisationId, active flag and UTC timestamps. Supported types: Platform, Ministry, Province, District, School, Partner, Project, Other. Codes allow letters/numbers with hyphen/underscore separators and are unique. Parent types are flexible. Self-parenting has a database constraint; ancestry cycles are checked with one projected ancestry query inside a serializable transaction. Changing a parent requires authority over the proposed parent as well as the child. Creating organisations and changing organisational active state are platform operations. Only a Platform Administrator may convert a boundary into or out of Platform.

OrganisationMembership connects UserId and OrganisationId with its own GUID, active flag, JoinedAt and timestamps. One membership exists per user/organisation, including inactive memberships. Reactivation preserves JoinedAt and roles. Memberships do not require roles. OrganisationMembershipRole assigns multiple roles per membership and records CreatedAt. RolePermission maps roles to permissions. Both joins have GUID IDs and unique pair constraints. All foreign keys use restrictive deletion. No user, organisation or membership hard-delete endpoint exists; role assignments can be removed. UpdatedAt concurrency checks and serializable transactions protect management changes; concurrent PostgreSQL uniqueness, FK and serialization failures become 409 conflicts.

## Reference roles and permissions

EF `HasData` defines 9 roles, 11 permissions and 45 mappings with fixed GUIDs and a fixed UTC reference timestamp. Migrations insert the deterministic reference rows once and future migrations can update them deliberately. Runtime startup does not repeatedly reseed them. No users, organisations or passwords are migration seed data. Do not reorder the reference code arrays or change existing identifiers; append definitions and create a reviewed migration when extending the catalogue.

| Stable role code | Initial permission grants |
| --- | --- |
| PlatformAdmin | All permissions; platform authority from an active membership in an active Platform organisation |
| OrganisationAdmin | All permissions within its organisation; no platform authority |
| ProjectManager | curriculum.read, content.read, users.read, organisations.read, memberships.manage, reports.read |
| HeadTeacher | curriculum.read, content.read, users.read, organisations.read, reports.read |
| Teacher | curriculum.read, content.read, organisations.read |
| Learner | curriculum.read, content.read |
| ParentGuardian | content.read |
| ContentManager | curriculum.read, content.read, content.manage, content.publish |
| DataAnalyst | reports.read, organisations.read |

Permission codes: `curriculum.read`, `curriculum.manage`, `content.read`, `content.manage`, `content.publish`, `users.read`, `users.manage`, `organisations.read`, `organisations.manage`, `memberships.manage`, `reports.read`. Authorization uses stable codes and the persisted GrantsPlatformAuthority capability, independent of display names. API endpoints expose reference definitions but do not edit them in this feature.

Delegating/removing a non-platform role requires the caller to possess all its permissions in that organisation, in addition to memberships.manage. Only Platform Administrators can assign/remove platform authority or change active state on memberships holding it. A platform role may only be assigned in a Platform organisation. State changes and role removal cannot eliminate the last active Platform Administrator. These checks run in serializable transactions; lockout is temporary and is not bypassed by this guard.

## Authentication and security configuration

Required configuration, supplied through environment variables, Development User Secrets or a production secret provider:

| Configuration | Environment variable | Requirement |
| --- | --- | --- |
| Jwt:Issuer | Jwt__Issuer | Absolute HTTPS issuer URI |
| Jwt:Audience | Jwt__Audience | Nonempty audience, maximum 200 characters |
| Jwt:SigningKeyBase64 | Jwt__SigningKeyBase64 | Cryptographically random Base64 key, at least 32 decoded bytes |
| Jwt:AccessTokenMinutes | Jwt__AccessTokenMinutes | 5–60 minutes; default 15 |
| ConnectionStrings:MwabuLearnDb | ConnectionStrings__MwabuLearnDb | Operator-provided PostgreSQL connection string |

The signing key is never stored in committed appsettings. Missing/weak JWT configuration fails startup. A local PowerShell process can generate a key without printing it using `$env:Jwt__SigningKeyBase64 = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))`; set issuer/audience separately and keep the process environment private. Keep production keys stable across instances/restarts and manage rotation operationally. The API project already has a UserSecretsId; Development User Secrets are outside source control and are for local development only. Do not paste secrets into shell history, documentation, tickets or source files.

Access tokens are HS256-signed using the standard JWT library. Validation requires issuer, audience, signature, expiry and HS256, with 30 seconds clock skew. Claims are sub (GUID), jti, iat, internal ver and standard issuer/audience/lifetime claims. No email, names, roles, membership graph or security stamp is embedded. Each authenticated request checks the current active user, token version and lockout state. User deactivation increments token version and changes the Identity security stamp; old tokens remain invalid after reactivation. Membership/organisation deactivation is checked by authorization on subsequent requests. No refresh token persistence, logout/session registry or password reset/email delivery is implemented. Refresh/session lifecycle and comprehensive security-stamp revocation for future password changes are a later security feature.

Identity defaults: minimum password length 12, at least 4 distinct characters, uppercase/lowercase/digit/symbol required; five failed attempts cause five-minute lockout. Configure through `Identity:Password:*` and `Identity:Lockout:*`; startup enforces length 12–128, at least four unique characters, 1–10 failed attempts and lockout duration greater than zero up to one hour. Unique email is always enabled. Initial passwords are accepted only in authenticated platform administration, validated by Identity, never returned or logged. Secure distribution of those initial credentials is an operator responsibility. Email is not yet verified, and no MFA/first-login forced password change is implemented; Identity's default token providers remain registered for later activation/recovery features.

Login returns the same generic 401 response for unknown, incorrect, inactive and locked-out accounts. Unknown/inactive/locked accounts perform dummy verification with Identity's standard hasher to reduce obvious timing differences. Login is rate limited per direct remote IP: 10 attempts/minute by default, configurable through Authentication:LoginAttemptsPerMinute (1–100). There is no custom cryptography or custom password storage. Do not enable request/response body logging on identity endpoints or log Authorization headers/tokens. Login responses use Cache-Control: no-store. Secrets and initial passwords must stay out of telemetry.

HTTPS redirection remains enabled; new identity/organisation controllers also require HTTPS. JWT HTTPS requirements are not disabled. A reverse proxy deployment must configure trusted forwarded headers/HTTPS scheme and real client-IP handling deliberately; no untrusted forwarded header is accepted here. Existing CORS behavior is preserved and needs a production origin allow-list in deployment. Swagger is enabled in Development and supports a bearer token via Authorize; protected operations carry security metadata.

These choices follow [Microsoft's JWT bearer guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0) and [Identity configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-8.0).

## Organisation context and authorization

`RequirePermission(code, PermissionScope.Organisation)` uses a dynamic ASP.NET Core policy and centralized requirement/handler. Prefer an explicit `{organisationId}` route value. For a future endpoint without that route, exactly one `X-Organisation-Id` header is required. If both are present, they must match. Invalid, empty, multiple or mismatched context fails authorization. The identifier is only a requested context: permission evaluation verifies active user, active membership, active organisation and role grants in the database.

Platform policies explicitly use `PermissionScope.Platform` and require active platform authority; a normal OrganisationAdmin cannot list/manage all users or organisations. Platform Administrators may access any active organisation without local membership. An inactive target organisation denies scoped access even for Platform Administrators, who can use the platform state endpoint to reactivate it. A missing target is 404 for a Platform Administrator and 403 for a caller without authority, avoiding disclosure to unrelated members. Role/permission snapshots are cached only within the current request by user/context; a single join fetches grants rather than querying each role/permission separately. Organisation active state is queried once per context. No role inheritance or permission inheritance from parent organisations is implemented. Parent deactivation does not recursively deactivate children; each organisational boundary is independently active.

## API endpoints

All routes are under `/api`. Requests use string enum names, for example `"organisationType": "School"`. All state PATCH requests require `{ "isActive": true/false }`. Creation returns 201 plus Location, reads/updates 200, state changes/removal 204; validation 400, authentication 401, authorization 403, missing resource 404 and conflicts 409 use ProblemDetails. Login throttling returns 429. User listing accepts Page/PageSize (maximum 100).

| Method / route | Required authority |
| --- | --- |
| POST /api/auth/login | Anonymous, HTTPS and rate limiting |
| GET /api/auth/me | Authenticated active user |
| GET /api/auth/me/memberships | Authenticated user's own memberships, organisations and role codes |
| GET /api/users | Platform users.read |
| GET /api/users/{id} | Platform users.read |
| POST /api/users | Platform users.manage; email, firstName, lastName, optional phoneNumber, initialPassword |
| PATCH /api/users/{id}/active | Platform users.manage |
| GET /api/organisations | Platform organisations.read |
| GET /api/organisations/{organisationId} | Scoped organisations.read |
| POST /api/organisations | Platform organisations.manage |
| PUT /api/organisations/{organisationId} | Scoped organisations.manage; additional parent/platform checks |
| PATCH /api/organisations/{organisationId}/active | Platform organisations.manage |
| GET /api/organisations/{organisationId}/members | Scoped users.read |
| POST /api/organisations/{organisationId}/members | Scoped memberships.manage; body userId |
| PATCH /api/organisations/{organisationId}/members/{membershipId}/active | Scoped memberships.manage; platform-role safeguard |
| GET /api/organisations/{organisationId}/members/{membershipId}/roles | Scoped memberships.manage |
| POST /api/organisations/{organisationId}/members/{membershipId}/roles | Scoped memberships.manage plus delegation checks; body roleId |
| DELETE /api/organisations/{organisationId}/members/{membershipId}/roles/{roleId} | Scoped memberships.manage plus delegation/last-admin checks |
| GET /api/roles | Authenticated reference catalogue |
| GET /api/permissions | Authenticated reference catalogue |

There is no public registration endpoint. Global user profiles are intentionally platform-only. A scoped member list returns membership IDs/user IDs/state/timestamps rather than unrestricted personal profiles. Organisation/member lists are currently unpaged; add bounded pagination before large-scale deployment if needed.

## First administrator bootstrap

Bootstrap is disabled by default and does no database work while disabled. It is a hosted startup operation, not a public API. Before enabling it, manually apply the reviewed migration and configure JWT, database and these keys via a private environment/Development User Secrets:

| Configuration key | Environment variable |
| --- | --- |
| BootstrapAdministrator:Enabled | BootstrapAdministrator__Enabled |
| BootstrapAdministrator:Email | BootstrapAdministrator__Email |
| BootstrapAdministrator:Password | BootstrapAdministrator__Password |
| BootstrapAdministrator:FirstName | BootstrapAdministrator__FirstName |
| BootstrapAdministrator:LastName | BootstrapAdministrator__LastName |
| BootstrapAdministrator:OrganisationName | BootstrapAdministrator__OrganisationName |
| BootstrapAdministrator:OrganisationCode | BootstrapAdministrator__OrganisationCode |

Set Enabled explicitly to true for the controlled initialization. Supply a real email and a newly generated secret from a password manager: length 16–128 with upper/lower/digit/symbol. Empty fields, example.com/localhost addresses and obvious default password phrases are refused; configured Identity password validation also applies. No example password is supplied. OrganisationCode follows the normal code rules. Bootstrap creates one user, one active Platform organisation (or uses an existing active matching Platform organisation), one membership and PlatformAdmin assignment atomically.

Once any platform authority assignment exists, subsequent runs do nothing: passwords are never reset, accounts/memberships are never reactivated and another administrator is never created. If users already exist without a platform authority assignment, startup refuses bootstrap takeover and requires operator review. Disabled bootstrap needs no credentials. After successful creation, set Enabled=false and remove bootstrap secrets from the environment/secret provider; keep JWT configuration. Verify login and provision another trusted administrator through the authenticated APIs. Bootstrap never runs an EF database migration. It is not a recovery/backdoor mechanism for an existing database.

## Curriculum/Content integration

Catalogue authorization is now implemented. See [catalogue-authorization.md](catalogue-authorization.md) for authenticated catalogue reads, platform management and separate publication permission requirements. Organisation-owned content and licensing remain future features.

## Verification and review decisions

Tests use relational SQLite databases with real Identity stores/password verification and HTTP TestServer JWT middleware; no PostgreSQL migration was applied. PostgreSQL model/migration metadata tests verify snapshot consistency, additive operations, standard Identity indexes, restrictive foreign keys and deterministic seeds. SQLite exercises uniqueness, membership/role/permission constraints, self-parent constraints and restricted deletion. PostgreSQL serialization/concurrency behavior still warrants operator review and staging verification against a disposable PostgreSQL database.

Coverage includes user creation/normalization, duplicate email, invalid passwords, login success/failure/inactive users/lockout, JWT claims/issuer/audience/signature/lifetime, me, 401/403, PlatformAdmin, scoped authority/isolation/header mismatch, org creation/duplicates/parent/self/cycles, memberships/duplicates/deactivation, roles/duplicates/removal/delegation, prevention of platform membership reactivation by local administrators, last-administrator safeguards, secure/disabled/idempotent/takeover-refusing bootstrap, Swagger and all existing Curriculum/Content tests.

Final verification: `dotnet restore backend/MwabuLearn.slnx`, `dotnet build backend/MwabuLearn.slnx --no-restore`, `dotnet test backend/MwabuLearn.slnx --no-build --no-restore`. Final result: restore succeeded; build succeeded with 0 warnings and 0 errors; all 129 tests passed (99 existing tests and 30 new cases), with 0 failed and 0 skipped. No package-source or TLS configuration was changed.

Human review: approve role grants, platform-only global administration, absence of parent inheritance, credential delivery/email verification/MFA/session lifecycle roadmap, production key management/rotation, trusted proxy/rate-limit setup and protection of the existing global content/curriculum management routes. The migration requires manual application. This branch adds no UI, sync, social login, reset-email delivery, full audit logging or unrelated module changes.

## Changed files

Added: Domain organisation entities; Application Identity DTOs/interfaces; Infrastructure Identity/auth/bootstrap/reference data/security services; organisation and membership services; explicit EF configurations; new migration and designer; API auth/users/organisations/catalogue controllers, exception handler and reusable security registration/policies/Swagger filter; relational/service/security/HTTP tests and shared test configuration; this document.

Modified: API Program.cs and appsettings.json (nonsecret bootstrap/rate defaults), Infrastructure package references, MwabuDbContext and model snapshot, existing CurriculumApiTests/ContentApiTests factories (ephemeral JWT test configuration). The previous three migrations are unchanged. The exact file inventory is recorded in the feature commit; generated .vs/bin/obj/TestResults, local databases/media and secrets are excluded.

### Exact inventory

Added:

- `backend/docs/identity-organisations.md`
- `backend/src/MwabuLearn.Api/Controllers/AccessCatalogController.cs`
- `backend/src/MwabuLearn.Api/Controllers/AuthController.cs`
- `backend/src/MwabuLearn.Api/Controllers/OrganisationsController.cs`
- `backend/src/MwabuLearn.Api/Controllers/UsersController.cs`
- `backend/src/MwabuLearn.Api/Errors/IdentityExceptionHandler.cs`
- `backend/src/MwabuLearn.Api/Security/AuthenticationRegistration.cs`
- `backend/src/MwabuLearn.Api/Security/BearerOperationFilter.cs`
- `backend/src/MwabuLearn.Api/Security/PermissionAuthorization.cs`
- `backend/src/MwabuLearn.Application/Identity/Contracts.cs`
- `backend/src/MwabuLearn.Application/Identity/Services.cs`
- `backend/src/MwabuLearn.Domain/Entities/Organisations/Organisation.cs`
- `backend/src/MwabuLearn.Domain/Entities/Organisations/OrganisationRole.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/ApplicationUser.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/AuthenticationService.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/BootstrapAdministrator.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/IdentityReferenceData.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/IdentityServiceRegistration.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/IdentityValidation.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/JwtTokens.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/PermissionEvaluator.cs`
- `backend/src/MwabuLearn.Infrastructure/Identity/UserService.cs`
- `backend/src/MwabuLearn.Infrastructure/Organisations/OrganisationService.Memberships.cs`
- `backend/src/MwabuLearn.Infrastructure/Organisations/OrganisationService.cs`
- `backend/src/MwabuLearn.Infrastructure/Persistence/Configurations/Identity/IdentityConfigurations.cs`
- `backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/20261006200525_IdentityOrganisations.Designer.cs`
- `backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/20261006200525_IdentityOrganisations.cs`
- `backend/tests/MwabuLearn.Tests/IdentityApiTests.cs`
- `backend/tests/MwabuLearn.Tests/IdentityModelTests.cs`
- `backend/tests/MwabuLearn.Tests/IdentitySecurityTests.cs`
- `backend/tests/MwabuLearn.Tests/IdentityServiceTests.cs`
- `backend/tests/MwabuLearn.Tests/IdentityTestEnvironment.cs`
- `backend/tests/MwabuLearn.Tests/OrganisationServiceTests.cs`
- `backend/tests/MwabuLearn.Tests/TestSecurityConfiguration.cs`

Modified:

- `backend/src/MwabuLearn.Api/Program.cs`
- `backend/src/MwabuLearn.Api/appsettings.json`
- `backend/src/MwabuLearn.Infrastructure/MwabuLearn.Infrastructure.csproj`
- `backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/MwabuDbContextModelSnapshot.cs`
- `backend/src/MwabuLearn.Infrastructure/Persistence/MwabuDbContext.cs`
- `backend/tests/MwabuLearn.Tests/ContentApiTests.cs`
- `backend/tests/MwabuLearn.Tests/CurriculumApiTests.cs`
