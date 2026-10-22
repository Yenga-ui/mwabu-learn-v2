# Guest access registration and licensing

## Product decisions

- Existing accounts keep their entitlements during the transition. This is a one-time migration decision, not evidence of a payment or a fabricated agreement acceptance.
- New accounts start unlicensed. Only platform administrators activate or revoke the manual licence flag.
- Learners **cannot self-register**. The final product-team clarification supersedes the earlier learner self-registration answer. A parent, teacher or Mwabu registers them on their behalf.
- The privacy document supplied with pre-loaded devices has not been supplied or verified. No replacement terms have been invented or silently accepted.

## Guest experience

The sign-in screen links to `/guest?audience=teacher` and `/guest?audience=learner`. These routes do not create accounts, grant authenticated roles or expose private lesson drafts. Both experiences are public audience labels, not proof of teacher identity.

In Content studio, open a resource and find **Guest access**. A platform administrator can enable a teacher preview, learner preview and playback. Existing resources default to no guest access. Only Published resources can be enabled; the publication/review workflow is unchanged. Preview-only responses omit assets, and every playback request rechecks publication and the current guest flags. Archiving or revoking access blocks later reads. Content must have suitable public-sharing rights before enabling it.

Supported guest playback: native Mwabu interactive activities, PDF, PNG/JPEG, MP3 and MP4. Activity definitions are checksum/schema validated and rendered through the existing trusted activity engine. Guest answers are practice feedback only; no assessment attempts or identifiable progress are posted. Guest downloads of lesson packages and executable SCORM packages are not enabled. SCORM can be previewed as selected catalogue metadata, but guest execution needs a separately scoped sandbox implementation; the existing authenticated SCORM security is not bypassed. A resource with only unsupported assets cannot have guest playback enabled.

Previously downloaded public or offline files cannot be remotely recalled. Revocation controls future online access, not copies already delivered.

## Agreements and adult account requests

Platform administrator → **People** → **Public registration and agreements**:

1. Obtain the approved agreement/privacy wording applicable to v2 and the intended registration workflows.
2. Enter a unique version, title and the complete approved text. An HTTPS source link is optional when the source is a printed device policy.
3. Enable adult-managed learner requests only if the approved agreement covers the required authority and guardian consent.
4. Confirm the wording is approved and publish the version.

Until a version is published, `/register` displays a clear unavailable state and no account form. The legacy website's active privacy policy was inspected at [Mwabu Learn policies](https://learn.mwabu.com/admin/tool/policy/viewall.php); it refers to Moodle and social sign-in, so it was not automatically represented as an accurate v2 agreement.

Public registration offers Teacher and Parent, never Learner or administrative roles. The applicant must actively accept the current agreement. The server rechecks the version during the same transaction that creates the account. A changed version requires fresh review. The immutable policy text/version/SHA-256 and server acceptance time are retained. Passwords use the existing Identity rules and hashing; registration does not return credentials, issue a session, grant memberships or activate a licence.

The existing account model uses a unique email address as the sign-in identifier. Email ownership verification is currently a manual administration task; no email verification delivery was added. Do not invent learner email addresses to work around this requirement. Username-only pre-loaded-device provisioning needs a separate product decision if required.

## Adult-managed learner requests

An existing licensed Teacher or ParentGuardian with an active organisation membership can open **Your account → Register a learner**. Mwabu platform administrators can use this flow on behalf of a learner too.

The registering adult chooses an authorised active organisation, supplies the learner account details, declares authority/required guardian consent and accepts the applicable agreement. The server enforces the adult's role, licence and organisation scope and caps requests per adult per day. Anonymous callers, learners and unrelated organisations are rejected.

The request records the adult, organisation, agreement and acceptance time. It creates an unlicensed learner account, with no automatic organisation membership, role assignment or verified guardian relationship. Mwabu must review those separately. The People account detail links to the registering adult, requested organisation and exact accepted agreement for review.

## Licence administration

Platform administrator → **People** → filter **Licence status: Pending licence** → open an account:

1. Check account ownership, the requested account type, applicable consent and payment/entitlement.
2. Arrange the appropriate active organisation membership and role through the existing organisation tools. Parent relationships must be verified using the existing school workflow.
3. Choose **Activate licence** and confirm.

An unlicensed account cannot sign in to authenticated learning. Licences do not confer roles: activation does not turn a parent into a teacher or grant administrative permissions. Platform administrators retain management access so they can administer licences. Disabled accounts remain disabled even after licence activation.

**Remove licence** blocks learning access and invalidates existing access/refresh sessions. Licence changes record the administrator, time and an audit event. Publication permissions, organisation isolation and AI approval/budget controls remain in force. This is manual entitlement administration; no payment gateway, invoicing, expiry scheduler or offline DRM is claimed.

## Migrations and local rollout

Two additive migrations: `GuestRegistrationAndLicensing` and `AdultManagedLearnerRegistration`. Existing migrations and permission IDs are unchanged. The first migration retains existing users' licence status as explicitly authorised, leaves existing agreement acceptance empty and defaults all future account inserts to unlicensed. Guest flags default off. The second adds adult-request provenance with restrictive foreign keys.

Application database changes require a verified local backup first. No production database, deployment or branch merge is authorised by this work. The local startup command remains `scripts/start-local-review.cmd`; final rollout details and verification results are appended below.

## Verification

- .NET solution build, TypeScript, strict ESLint and production frontend build passed.
- Full frontend suite: 92 tests passed.
- Final full backend suite: 368 passed, zero skipped, including the PostgreSQL upgrade-retention test.
- Focused licence tests include access-token and refresh-token revocation, unauthorised grant rejection, default unlicensed registration and current-policy acceptance.
- Adult-managed tests reject anonymous/learner callers, missing consent and foreign organisation requests, and verify no automatic role/guardian grants.
- API documentation explicitly distinguishes the five intended public operations from the protected guest/registration administration operations; the original catalogue authentication assertions remain.
- Dependency audits: eight .NET projects, no vulnerable direct/transitive packages; frontend audit zero advisories at all severities.
- Guest preview/revocation, adult registration, manual licence activation and teacher-managed learner registration were exercised through the browser against an isolated PostgreSQL application using synthetic test agreements only.
- Initial browser failures exposed the missing SPA allow-list entries for the two new public pages; fixed without adding a catch-all API fallback. A quiz practice feedback accessibility label was made consistent with other activities.
- Final full browser suite: 60 passed, including the real Product Team SCORM package, guest activity playback, adult account requests, teacher-managed learner requests and existing teacher workflows.

## Actual local application verification

Both additive migrations were applied to the existing loopback `mwabu_learn` database after a custom-format backup, archive-list verification and SHA-256 recording. Backup metadata is retained locally in `.local/backups/access-backup.json`; backups are ignored by Git. No production database was accessed.

The application was restarted using `.local/access-api` and `.local/access-web`; `scripts/start-local-review.cmd` now uses those builds. Run that CMD file from Explorer or a terminal, without `next`, to start it when stopped. The working directory remains the API source folder, preserving existing content storage. URL: `https://localhost:7042`; readiness returned healthy. EF reports no pending model changes.

The actual existing teacher session successfully reopened its saved lesson list after restart. Actual `/guest` displays no samples because all guest flags remain off, and `/register` displays registration unavailable because no approved agreement has been published. No synthetic test agreement or new public content grant was added to the existing local database. Browser evidence: `artifacts/guest-access-2026-10-22/registration-awaits-approved-policy.png` (local ignored artifact).

## Outstanding before public release

- Obtain and approve the applicable privacy/terms document, particularly guardian consent and pre-loaded-device wording. Public registration stays disabled in the actual local application until an administrator publishes approved text.
- A platform administrator must explicitly choose the public sample materials. None are automatically exposed.
- Guest SCORM execution, payment processing, automated expiry, email verification delivery and username-only device provisioning are not included in this manual-licensing iteration.
