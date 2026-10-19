# PR #4 merge conflict resolution

The feature branch retained the original Sprint 1 commits; main contains the identical Sprint 1 tree as squash commit 6bcd01c. Comparing origin/main with 6036c35 produced no differences. Merging main therefore required reconciling duplicate ancestry, with the complete Sprint 2 implementations retained.

The resolved source tree is identical to pre-merge feature commit 6714bcd. All historical migrations, permission identifiers and grants, routes, tests, activity contracts and AI job behavior are preserved. No schema changes or new migrations are required for this merge.

## Conflicted files

- backend/src/MwabuLearn.Api/Program.cs
- backend/src/MwabuLearn.Application/Content/Activities.cs
- backend/src/MwabuLearn.Domain/Entities/Lessons/LessonGenerationJob.cs
- backend/src/MwabuLearn.Infrastructure/Lessons/LessonConfigurations.cs
- backend/src/MwabuLearn.Infrastructure/Lessons/LessonGenerationJobs.cs
- backend/src/MwabuLearn.Infrastructure/Lessons/LessonGenerationWorker.cs
- backend/src/MwabuLearn.Infrastructure/Lessons/LessonRegistration.cs
- backend/src/MwabuLearn.Infrastructure/Persistence/Migrations/MwabuDbContextModelSnapshot.cs
- backend/tests/MwabuLearn.Tests/ActivityTests.cs
- docs/content-activities.md
- docs/lesson-platform-next-implementation-plan.md
- docs/teacher-ai-generation.md
- docs/teacher-platform-readiness.md
- web/e2e/activities.spec.ts
- web/src/app/styles.css
- web/src/features/content/Resource.tsx
- web/src/features/content/activities/ActivityComposer.tsx
- web/src/features/content/activities/activities.test.tsx
- web/src/features/content/activities/activityRuntime.ts
- web/src/features/content/activities/offlineActivity.ts
- web/src/features/curriculum/NodePicker.test.tsx
- web/src/features/curriculum/NodePicker.tsx

## Verification

Local verification: backend 319 passed, zero skipped; frontend 59 passed; Chromium browser workflows 47 passed. Frontend lint/type checking and production build passed. Release .NET build passed with zero warnings/errors. EF reported no pending model changes and generated the full idempotent migration script. Direct/transitive NuGet and web audits found no known vulnerabilities. All migrations already on main remain byte-for-byte unchanged. Browser tooling emitted a harmless NO_COLOR/FORCE_COLOR warning. The merge does not authorize deployment, production migrations, or merging the pull request.
