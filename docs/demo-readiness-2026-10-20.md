# Presentation handoff — 20 October 2026, 12:00 CAT

**Online demonstration rehearsed successfully in the actual running application. The presenter confirmed offline interaction works, but Chrome blocks the embedded PDF guide. Use the separate PDF fallback below.** This is a local demonstration environment, not a production deployment. Physical projector/display readability has not been verified.

Final local checks: **09:15 CAT**. API healthy; no Error/Critical log entries or HTTP 5xx responses in the post-restart log; nine screenshots preserved. The API remains running as PID **30292**, and the authenticated lesson-library browser tab is left open.

## 1. Environment and exact startup

Repository: `C:\Development\mwabu-learn-v2`. Branch: `feature/classroom-readiness`. Application commit: `e3cc0025ab972a13f5041a138f3c83e8e91767e8`. No application source changes were required for this rehearsal.

PostgreSQL 17 service `postgresql-x64-17` was already running on port 5432. Existing database `mwabu_learn` was inspected **read-only**: it contains the Zambia National Curriculum, Grade 4, and an existing administrator, but its migration chain predates Sprint 2. Its data, credentials and schema were left unchanged. A **new** database, `mwabu_demo_20261020`, was created and migrated through `20261019161015_AssessmentRoleDelegation`. Only this isolated database received demonstration records.

The real Release API executable serves the existing built React frontend from `web\dist` over the same trusted HTTPS origin. No Vite process, browser-test host, mock AI provider or external connectivity is required for the presenter journey.

Run in PowerShell under the current Windows user (`Ctrl Hub Zambia`):

```powershell
Set-Location 'C:\Development\mwabu-learn-v2'
Get-Service postgresql-x64-17
& '.\.local\demo-2026-10-20\Start-Demo.ps1'
Invoke-RestMethod 'https://localhost:7042/health/ready'
Start-Process 'https://localhost:7042/app/lessons'
```

Expected health response: `status: healthy`. The launcher is idempotent while its recorded API process is running. PostgreSQL must remain running. If its service has stopped, an operator can start that existing local service with `Start-Service postgresql-x64-17`; do not reinitialize PostgreSQL or recreate either database.

The launch script sets these nonsecret configuration values before starting the real executable in a hidden window:

```text
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:7042
ConnectionStrings__MwabuLearnDb=Host=127.0.0.1;Port=5432;Database=mwabu_demo_20261020;Username=postgres
ContentStorage__RootPath=C:\Development\mwabu-learn-v2\.local\demo-2026-10-20\content
BootstrapAdministrator__Enabled=false
DevelopmentSeed__Enabled=false
LessonPlanner__Ai__Enabled=false
```

Executable: `backend\src\MwabuLearn.Api\bin\Release\net10.0\MwabuLearn.Api.exe`; working directory: `backend\src\MwabuLearn.Api`; arguments: `--webroot C:\Development\mwabu-learn-v2\web\dist`. Existing development JWT configuration remains in this Windows user's ASP.NET User Secrets. Do not print or copy it into presentation materials. HTTPS development certificate trust was explicitly approved by the user and verified; no certificate-validation bypass was used.

Local helpers and state, all ignored by Git:

- `.local\demo-2026-10-20\Start-Demo.ps1` — restart the prepared environment; does not run migrations or seed data.
- `Copy-DemoPassword.ps1` — copies a chosen local demonstration credential to the Windows clipboard without printing it.
- `credentials.xml` — Windows DPAPI-protected credentials, readable by the preparing Windows user; never commit, project, or share this file.
- `api.pid`, `api.log`, `api-error.log` — exact demo process and logs.
- `lesson.json`, `resources.json`, `seed.json`, `package-manifest.json`, `migrations.log` — local preparation evidence.
- `demo-backup.dump` — backup of **only** the isolated demo database (192,500 bytes when created). Do not restore over an existing database during the presentation.

The launcher and existing build are already prepared on this machine. Do not rebuild frontend assets while the application is serving them. Do not rerun the one-time preparation scripts or seed endpoint before the presentation.

## 2. URLs

| Purpose | URL |
| --- | --- |
| Frontend / sign-in | https://localhost:7042/ |
| Lesson library | https://localhost:7042/app/lessons |
| API base | https://localhost:7042/api |
| Readiness | https://localhost:7042/health/ready |
| API documentation (Development) | https://localhost:7042/swagger |
| Editable presenter plan | https://localhost:7042/app/lessons/0669769b-1f26-4fba-b6bb-8e9eee6b7099 |
| Published plan | https://localhost:7042/app/lessons/fc362433-0663-4651-abf1-dd4ed0bedb6d |
| Published preparation | https://localhost:7042/app/lessons/fc362433-0663-4651-abf1-dd4ed0bedb6d/journey?revision=5 |
| Published classroom fallback | https://localhost:7042/app/lessons/fc362433-0663-4651-abf1-dd4ed0bedb6d/teach?revision=5 |

## 3. Account and approval provenance

Presenter account: **`demo.teacher@mwabu.invalid`**, Teacher, **Chipo Teacher**, organisation **Riverside Demonstration School**. Organisation ID: `a4b1a54d-f670-4bab-8011-c3b8ab67505f`. No real learner records or identifiable progress are required.

Before sign-in, privately run:

```powershell
& 'C:\Development\mwabu-learn-v2\.local\demo-2026-10-20\Copy-DemoPassword.ps1' -Role Teacher
```

Paste into the application's Password field. Never show the clipboard/password to the audience. After sign-in, clear it with `Set-Clipboard -Value ''`. Passwords are intentionally absent from this document.

Supported Development seeding provisioned eight distinct role accounts and original demonstration curriculum/PDF records. The manually authored activity definitions were submitted by `demo.contentmanager@mwabu.invalid`, approved by the separate `demo.platformadmin@mwabu.invalid` account through the normal approval API, then published by the content manager. The lesson was submitted by the teacher and published by `demo.headteacher@mwabu.invalid` through the normal lesson review transition. No database status edits, self-approval, permission changes or publishing bypasses were used. These approvals concern original synthetic demonstration material only.

## 4. Exact lesson and presenter walkthrough

Published title: **DEMO 2026-10-20 — Grade 4 Mathematics: Fractions — Equal Shares**; published revision **5**. The editable version has the exact same title followed by **` (copy)`**, is a **Draft**, and was rehearsed at revision **2**. Use the copy when demonstrating Save; the published lesson is deliberately immutable and remains available for offline export.

Curriculum path: **Demonstration Learning Framework → Demonstration 2026 → Grade 4 → Mathematics → Term 1 → Fractions in everyday life → Recognise equal parts → Represent one half using familiar objects**. These are real managed records in the isolated database. This original demonstration framework is **not presented as an official national syllabus**.

Objectives: recognise equal shares; represent one half using two of four equal parts; explain why two quarters equal one half. Five steps total 40 minutes (5 + 10 + 10 + 10 + 5). Compress discussion for a 7–10 minute stakeholder demonstration.

1. Sign in as the Teacher. Confirm **Riverside Demonstration School** in the organisation selector. Open **Lesson Planner** / the lesson library.
2. Click the title ending **`(copy)`** to show the Lesson Plan, curriculum context, objectives and **40 / 40 minutes**. Do not use the published plan for live editing.
3. Click **Prepare Lesson Journey**. Show the five-step timeline and the separation between **Teacher instructions (private)** and **Learner-facing explanation**.
4. Select **2. Build one half · 10 min**. Under **Linked approved resources**, click **Preview DEMO 2026-10-20 - Explore halves**. Explain that the attachment is pinned to a published asset version. The other approved activity is linked to step 4; the published PDF guide is linked to step 1.
5. Add a short practical note to **Teacher notes (private)**, for example “Paper circles ready.” Click **Save preparation**. Confirm the saved revision increases and the Save button becomes disabled. The revision in the Teach link updates automatically; do not keep using a stale copied URL after an edit.
6. Click **Teach Lesson**. Begin with **Sharing fairly**. Read the two-friends sharing prompt. The classroom screen excludes teacher instructions and notes. If mirroring, enter classroom mode before learners can see the private preparation screen.
7. Click **Next step** to **Build one half**. Select **DEMO 2026-10-20 - Explore halves** in **Teaching resource**. Click **Part 1**, then **Part 2**: the visual reads **2 / 4 shaded**. Ask why this is half. Click **Reveal activity answer**, then **Show explanation**. Use **Reset demonstration** to clear it. The demonstration records no learner scores.
8. Click **Next step** for **Fold and explain**. Briefly demonstrate folding paper into quarters and shading two. The paper exercise is also the low-technology fallback.
9. Click **Next step** for **Discuss the fraction question**. Ask the cake question before revealing anything. Click **Show hint** only if needed. Select the **Equal shares question** teaching resource. Discuss/select **One half**, then click **Reveal activity answer** and **Show explanation**. The separate classroom **Reveal answer** control reveals the saved step answer; both reveal paths were tested.
10. Click **Next step** for **Recap: equal parts make halves**. Summarise equal parts and `2/4 = 1/2`. Click **Finish lesson** and confirm return to the library.
11. Optional offline segment: open the **Published** plan, expand **Complete offline lesson package**, confirm the no-personal-data checkbox, and click **Download complete lesson package**. Do not confuse this with the separate text-only **Download offline copy** button. The already exported complete package is listed below.

Keep the published revision 5 classroom URL bookmarked as the immediate online fallback if a live edit is interrupted. Fullscreen entry/exit controls toggled correctly in rehearsal, but the embedded browser remained narrow; manually check the actual presentation browser and projector before noon.

## 5. Screenshots and offline fallback files

All paths below are under `C:\Development\mwabu-learn-v2\docs\demo-2026-10-20\`. Screenshots are from the actual application. The embedded browser's narrow viewport means some show scrolled portions; they are evidence/fallback views, not a verified projector layout.

| File | Screen |
| --- | --- |
| `00-lesson-library.jpg` | Draft presenter copy and Published original |
| `01-lesson-plan.jpg` | Saved published plan |
| `02-prepare-journey.jpg` | Preparation and curriculum |
| `03-teach-introduction.jpg` | Learner-facing introduction |
| `04-interactive-fractions.jpg` | Two of four equal parts selected |
| `05-question-answer-explanation.jpg` | Controlled quiz answer and explanation |
| `06-recap.jpg` | Recap |
| `07-offline-export-verified.jpg` | Successful verified package export |
| `08-linked-content-preview.jpg` | Linked activity preview |
| `fractions-offline-revision-5.html` | Complete self-contained exported lesson package |
| `sharing-equally-guide.pdf` | Verified standalone PDF fallback for the blocked inline viewer |

Offline package: **21,716 bytes**, revision **5**, manifest version `c59e64417ebe46a746af5053c093cb28190c9c38cd7a619ae6e8dc9e68c77116`, three pinned assets (PDF, fraction activity, question activity), five public teaching steps. File SHA-256: `E85D02FF341F8E67214ED253E0DDCFBAA8229D77B3D4F56E5A02889C663DD93B`.

**Presenter confirmation received:** in response to the offline-file/disconnected activity test request, the presenter reported “Tested. It works” and supplied a Chrome screenshot showing that the blocked middle section is the embedded `sharing-equally.pdf` guide. Offline interaction is presenter-confirmed; embedded PDF display is a reproducible limitation. Automation itself could not open `file://` URLs and that policy was not bypassed. The renderer places PDFs in a sandboxed data-URL iframe; this is consistent with the Chrome viewer failure, although no Chrome console diagnosis was obtained.

**PDF fallback:** `C:\Development\mwabu-learn-v2\docs\demo-2026-10-20\sharing-equally-guide.pdf` was extracted from the approved package without modifying the HTML or its security policy. Its 1,088 bytes match the pinned asset SHA-256 `8e93b51d62b844689090667423d405462030efb22278f257379975fe5e6e2442`. Open it separately in a PDF reader or print it before the demonstration. Standalone-reader display has not been independently observed by the agent. Skip the blocked inline PDF pane during the presentation; use the working interactive fraction/question sections.

## 6. What was actually tested

- Real PostgreSQL 17 + Release API + built frontend running together over trusted HTTPS; `/health/ready` healthy.
- Real teacher browser login, organisation selection and managed curriculum selections through all eight levels; real draft creation in the UI.
- Full original lesson saved through the normal API with five timed steps and published pinned resources. Preparation edited and saved in the actual browser (revision 2 → 3 before review).
- Full browser journey: library → plan → preparation → linked activity preview → Save → Teach → interactive fraction manipulation → controlled hint/answer/explanation → guided practice → recap → Finish → library.
- Published status confirmed in the library at revision 5, after distinct author/reviewer transitions. Presenter copy created using **Duplicate lesson**, then browser-edited/saved from revision 1 → 2 and taught through Finish.
- Fraction selection showed **2 / 4 shaded**; reset showed **0 / 4 shaded**. Quiz revealed **One half** and the authored two-quarters explanation. Explanations were disabled until the activity answer was revealed.
- Complete package downloaded through the actual UI; UI reported **Verified package exported**. Manifest rechecked at revision 5 with three assets. Standalone file preserved and SHA-256 recorded.
- Exact demo API process restarted using the prepared launcher; existing browser session remained authenticated and published lesson loaded from persistent storage. API health and package manifest remained valid.
- Published revision 5 was then rehearsed again through fraction interaction, question hint/answer/explanation, recap and Finish, after that restart.
- Classroom API projection checked for absence of `teacherInstructions`, `teacherNotes` and private rehearsal guidance. No learner scores/progress were collected by classroom interactions.
- PDF selection rendered an iframe in its assigned introduction step; browser console had no warning/error entries at that check. PDF bytes were included in the verified package. Detailed PDF viewer rendering on the eventual projector remains a manual check.

No application bug fixes were necessary; no application code, permission definitions or migrations were changed. Full automated suites were not rerun for this data/documentation-only rehearsal. Prior Sprint 3 CI results are separate evidence, not a substitute for the actual browser tests above. No branch merge, deployment or production migration occurred.

## 7. Blockers, limitations and warnings

- **Offline PDF viewer:** presenter confirmed offline interaction works, but Chrome blocks the embedded guide. A separate checksum-verified PDF is provided; use a PDF reader or printed guide. Do not claim the inline viewer passed.
- **Presentation display:** physical projector, browser window size, legibility and mirroring remain unverified. The attempted browser viewport override did not change its actual embedded width; no wide-screen acceptance claim is made.
- **Curriculum provenance:** the isolated demo uses explicitly labelled original demonstration records, not a verified official national curriculum copy. Existing original database data was not upgraded or repurposed.
- **AI:** deliberately disabled for this isolated launch. No live model request/latency claim is part of the demo. The saved manually authored and reviewed lesson works without AI.
- API logs emitted the existing ASP.NET antiforgery warning that Cache-Control/Pragma were replaced with `no-cache, no-store` for CSRF responses. This protects those responses; it was not suppressed. No API Error/Critical entries were observed in the rehearsal log review.
- The setup is local to this Windows user and machine. The local credential helper and existing User Secrets are prerequisites; copying the document alone to another machine does not recreate the environment.

## 8. Failure fallback and final checks before noon

1. **AI unavailable:** continue with the saved lesson; do not wait for a live proposal. Explain that proposals require teacher review. Never present seeded/static content as live AI output.
2. **Internet unavailable:** the live app is local and needs no internet for this lesson. Keep PostgreSQL and the API running. If the API also fails, use the presenter-tested exported HTML for activities and the separate PDF/printed guide; avoid the blocked inline PDF pane. Screenshots and folded paper are the final fallback.
3. **Media/PDF viewer unavailable:** skip the blocked inline PDF pane; open `sharing-equally-guide.pdf` in a separate PDF reader or use a printed copy. Use the saved learner explanation and physical paper circles; both interactive activities are local published JSON resources and do not require a video provider.
4. **Edit/save interrupted:** reopen the library and latest draft revision; use the stable published revision 5 Teach URL immediately if necessary. Do not alter the published original or force an approval.
5. **Session expired:** privately copy the Teacher password with the helper and sign in again. Never switch the presenter to platform administrator merely to work around a permission error.
6. **API unavailable:** run the launcher and readiness check above. Inspect only the local demo logs. Do not stop unrelated processes, change production connection strings, reset databases or rerun migrations as a presentation shortcut.
7. **Display fails:** use the printed paper-sharing exercise and captured screenshots. Before audience entry, open the actual presentation browser, maximise it, test **Enter fullscreen**, and check the interactive controls are visible/readable on the projector.

The app and isolated demo database are left running. Keep the laptop awake and powered until the presentation. Clear the password clipboard, close private account/configuration windows, open the lesson library, and confirm both Draft and Published cards are visible immediately before starting.
