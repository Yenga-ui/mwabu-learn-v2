# Mwabu Learn v2: Production Backend Prompt Pack

These are five staged prompts for taking the Mwabu Learn v2 backend to production. Each file is self-contained: the shared context (repo facts, deployment reality, binding architecture decisions, quality bar, working method, evidence rules) sits at the top of every stage, so you can paste a file straight into a fresh coding-agent session.

## How to run

| Order | File | Branch | Depends on |
|---|---|---|---|
| 1 | `stage-1-foundation.md` | `feature/prod-stage-1-foundation` | main |
| 2 | `stage-2-identity-audit.md` | `feature/prod-stage-2-identity` | Stage 1 |
| 3 | `stage-3-data-storage-jobs.md` | `feature/prod-stage-3-data-storage` | Stage 2 |
| 4 | `stage-4-devices-sync.md` (can run as 4A + 4B) | `feature/prod-stage-4-devices-sync` | Stage 3 |
| 5 | `stage-5-readiness.md` | `feature/prod-stage-5-readiness` | Stage 4 |

1. Start a **fresh agent session** for each stage, so a stage never inherits a half-used context.
2. Paste the whole stage file as the prompt.
3. When the stage finishes, **review the PR yourself**. Check the report's explicit yes/no answers and the evidence for them, and read the generated migration SQL.
4. Merge into main (preferred), or leave the branch open; the next stage branches from it either way.
5. Every stage reads and appends to `backend/docs/production/PROGRESS.md`, which carries decisions and handoffs between sessions.

If Stage 4 runs out of context, run it again with "You are running Stage 4A" (devices and credentials) and then "Stage 4B" (sync) added above the prompt.

## Decisions I made for you (please confirm before Stage 1)

These are in section C of every prompt, labelled AD-xx. The ones to look at closely:

- **AD-02**: The diagram's "Sync Database" lives as `sync`/`audit`/`ops` schemas in the same PostgreSQL database, not as a separate database.
- **AD-03 to AD-07**: Tablets get their own device identity and credential. Catalogue sync runs as the device, not the signed-in user, so a shared tablet stays current. User logins on a tablet are bound to the device through a device token. The tablet receives a "lock this profile" stream when a teacher is deactivated.
- **AD-08**: Refresh-token retry grace window (default 120 s), for lost responses on bad networks.
- **AD-09/10**: Every device gets all *published* catalogue metadata. Media is downloaded selectively through manifests. Licensing and org-owned content stay deferred.
- **AD-12**: Commit-safe change-feed ordering using PostgreSQL `xid8` and snapshot xmin. A real Postgres test must prove nothing is skipped.
- **AD-23**: New permissions and who gets them:
  - `audit.read`: PlatformAdmin only.
  - `devices.register`: OrganisationAdmin, ProjectManager, HeadTeacher, Teacher.
  - `devices.manage`: OrganisationAdmin, ProjectManager, HeadTeacher.
- **Default lifetimes**, all configurable:
  - User refresh sessions: 30 days idle, 180 days absolute.
  - Device credentials: 90 days idle, 365 days absolute.
  - Change-log retention: 180 days.
  - Reset tokens: 30 minutes.

## Open product questions (the agent is told not to invent answers)

- How learners without an email address or phone get accounts. Today every user needs an email.
- Licensing and entitlement rules: which organisation sees which content.
- The first upstream domain: lesson plans, attendance or assessment.
- Retention periods for audit, sessions and sync logs.
- The email/SMS provider for password reset. Until one is chosen, Production reset is deliberately non-functional and reported as HIGH.
