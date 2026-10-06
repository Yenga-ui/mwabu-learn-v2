# Catalogue authorization

Curriculum, Content, Collections and Tags are shared global catalogue resources. All 55 existing API operations now require authenticated JWT access; endpoint contracts and workflow rules are unchanged. No migration or reference-data change is required.

| Operation | Scope and permission |
| --- | --- |
| Curriculum GETs, including hierarchy and every structure node | Catalogue `curriculum.read` |
| Content GETs, search, assets/downloads, collections, tags and mappings | Catalogue `content.read` |
| Curriculum mutations | Platform `curriculum.manage` |
| Content, asset, collection, tag and mapping mutations; archive | Platform `content.manage` |
| Status transition into Published | Platform `content.manage` **and** Platform `content.publish` |

`PermissionScope.Catalogue` extends the existing permission policy provider/handler and `IPermissionEvaluator`. A catalogue read requires an active user with the requested permission through any active membership in an active organisation. It does not require a Platform organisation, PlatformAdmin or an organisation context. Multiple memberships are allowed; one valid grant is sufficient. An active membership without an appropriate role grant is insufficient. Catalogue scope accepts the two catalogue read codes only.

Catalogue checks use a server-side SQL EXISTS joining membership roles and role-permission mappings, without loading the graph. Results are cached per user/permission within the request only. Membership/organisation deactivation and role removal take effect on the next request with the same JWT. Existing JWT validation, account deactivation/lockout and token-version checks remain unchanged. Permissions are not added to tokens.

Platform policies require active platform authority **and the requested permission from a platform-authority role in an active Platform organisation**. Previously platform policies checked authority alone; checking the actual permission is the minimal fix needed to distinguish management from publication. Existing PlatformAdmin still works because its reference role grants all permissions. Ordinary organisation grants cannot supply missing global management/publication permissions, even when the user also holds a limited platform role. No role-name comparison is used in controllers.

Neither catalogue reads nor global writes use `X-Organisation-Id` or an organisation route context. Headers cannot select a global resource boundary or upgrade authority. Organisation policies on identity/organisation endpoints retain their existing context validation.

Every static permission is declared on its controller action. The status action first requires platform content.manage, then uses ASP.NET Core `IAuthorizationService` with the existing platform content.publish policy if the requested target is Published. Authorization runs before service workflow validation or mutation. `content.manage` alone cannot publish; `content.publish` alone cannot bypass management. Existing transitions remain Draft -> InReview, InReview -> Draft, InReview -> Published, Published -> Archived.

Unauthenticated, invalid/expired JWT or inactive user requests receive **401**. Authenticated users lacking the permission or an active qualifying membership/organisation receive **403**. Authorized invalid workflow transitions receive **409**. The existing ProblemDetails and bearer Swagger support are reused. Catalogue controllers require HTTPS; no authentication bypass is introduced.

## Reference-data findings

| Existing role | curriculum.read | content.read |
| --- | --- | --- |
| Teacher | Yes | Yes |
| Learner | Yes | Yes |
| HeadTeacher | Yes | Yes |
| ParentGuardian | No | Yes |
| ProjectManager | Yes | Yes |
| OrganisationAdmin | Yes | Yes |
| ContentManager | Yes | Yes |
| PlatformAdmin | Yes | Yes |
| DataAnalyst | No | No |

ParentGuardian's content-only access is preserved and tested. No permission mappings, deterministic GUIDs, EF model, snapshot or migrations were changed. A Teacher can read Curriculum/Content but cannot mutate either global catalogue; organisation-scoped management/publication grants do not change that. PlatformAdmin requires no organisation header.

## Verification and boundaries

HTTP integration tests exercise real Identity/JWT middleware with relational SQLite, all 55 routes, every consuming role above, full hierarchy and published content/asset downloads, 401/403, missing/disabled memberships and organisations, role removal with unchanged JWT, multiple memberships, platform writes, limited platform permissions, publication/workflow separation and Swagger security metadata. Limited platform roles are fixture-only data; seeded production definitions are untouched. Existing Curriculum/Content API tests now authenticate through real login with random test credentials; existing service/Identity/Organisation tests are retained.

Status visibility is unchanged: read-authorized users can still retrieve Draft/InReview/Archived resources through the existing read contracts. This feature establishes authenticated catalogue access; publication-based visibility, licensing and public access remain separate product/security decisions. Production proxy, key management, CORS and session-lifecycle considerations from the Identity delivery remain applicable. No PostgreSQL migration was created or applied.

Verification: `dotnet restore backend/MwabuLearn.slnx` succeeded; `dotnet build backend/MwabuLearn.slnx --no-restore` succeeded with 0 warnings/errors; `dotnet test backend/MwabuLearn.slnx --no-build --no-restore` passed all 145 tests (129 existing, 16 new cases), with no failures or skips.
