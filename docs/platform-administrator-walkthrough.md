# Platform administrator demonstration walkthrough

Prepared 20 October 2026 for the actual local Mwabu Learn application on `feature/classroom-readiness`, application commit `e3cc0025ab972a13f5041a138f3c83e8e91767e8`.

**Purpose:** demonstrate how a platform administrator establishes a school, controls access, curates curriculum and content, governs AI, supports teaching and monitors operations. This is a presenter script and preparation checklist, not a claim that every workflow below has been rehearsed in the current database.

## 1. Start here: application and available data

Open **https://localhost:7042/** and sign in with the existing **PlatformAdmin** account in the actual application. Do not use the `demo.*@mwabu.invalid` credentials: those belong to the separate, stopped demo database. Passwords are intentionally excluded from this document.

The actual application uses local database **`mwabu_learn`**. Its five pending migrations were applied with explicit approval after backup. Liveness, readiness and login endpoints passed; EF reported no pending model changes. The isolated demo database was not imported into it.

Read-only inventory taken while preparing this guide:

| Item | Current actual application data | Presentation consequence |
| --- | --- | --- |
| Platform organisation | Mwabu Education Zambia, type Platform | Use for platform administration/global catalogue work |
| Roles assigned | One PlatformAdmin membership and one Teacher membership | No learner-role membership is currently present |
| Curriculum | Zambia National Curriculum → Grade 4 → Mathematics; existing topic: Whole Number | Use this existing alignment for the teacher handoff; do not claim a managed Fractions outcome exists here |
| Lesson | Understanding Whole Numbers and Place Value — Draft | May be private to its author; demonstrate as that teacher, or submit/share through normal controls |
| Content | Fractions — Draft, type lesson | It will not appear in the published learner catalogue until properly reviewed/published |
| Projects / devices / question banks | None | Prepare examples or show the honest empty states |

**Current limits:** published-content delivery, learner assessment and a multi-school organisation demonstration require additional preparation. AI is enabled in the normal server configuration, but provider availability, organisation approval, quotas and a successful current proposal have not been verified in this walkthrough. Do not promise a live generation result until rehearsed.

### Suggested running order

| Segment | Full demonstration | Short version |
| --- | --- | --- |
| Sign-in, scope and dashboard | 3 min | 2 min |
| Organisations, people and roles | 10 min | 4 min |
| Curriculum and school assignment | 5 min | 2 min |
| Content Studio, activities and independent approval | 12 min | 4 min |
| AI governance and teacher handoff | 12 min plus measured generation time | 4 min using a previously reviewed proposal |
| Projects, assessments and operational oversight | 10 min | 3 min |
| Offline and closing summary | 5 min | 1 min |

The full route takes about an hour, depending on questions and AI latency. Use pre-created, reviewed records for the short route. The suggested timings are presentation allocations, not measured application performance.

## 2. Presenter preparation checklist

Complete these before the audience arrives. Creating demonstration data is a separate preparation activity; this guide did not create it in the actual database.

- Confirm https://localhost:7042/health/ready returns `healthy`.
- Confirm the administrator can see **People**, **Organisations**, **Content studio**, **Curriculum explorer**, **Reports** and **Audit trail**. A school OrganisationAdmin account is not a substitute for PlatformAdmin.
- Reserve clearly labelled sample records, for example **Presentation School — 20 Oct 2026** and **Presentation programme — 20 Oct 2026**. Keep them separate from existing school/user records. Use unique internal codes where required.
- Prepare separate accounts for an author, reviewer, teacher and learner. Store/share credentials privately. Do not reset existing accounts merely to make a demonstration easier.
- Ensure each account has an active membership and appropriate role in the intended organisation. Teacher and learner should share the demonstration school; the content author needs appropriate global catalogue access through a Platform organisation.
- Prepare one downloadable, licensed resource and one interactive activity. Review them for curriculum accuracy, language, accessibility, rights and absence of personal data before publication.
- Prepare a teacher-owned curriculum-aligned lesson with completed required sections, correctly totalled minutes and resources attached to the intended teaching steps.
- Rehearse any live AI request before presenting it. Confirm server/provider readiness, organisation approval and available budgets. Keep a saved reviewed lesson available if generation is slow or unavailable.
- For simultaneous role comparisons, use separate browser profiles or separate browser sessions. Ordinary tabs in the same browser session share authentication cookies; they are not independent role logins.
- Maximise the presentation browser and check projector readability. Enter **Teach Lesson** before showing a learner-facing display; private preparation instructions should not be projected to learners.

**Presenter opening:** “I will follow the administrator's work from school setup and access control to approved learning content, teacher support and operational oversight. I will switch to a teacher or learner account where the task belongs to that role.”

## 3. Sign in and explain organisation context

**Route:** `/app` — **Your workspace**.

1. Sign in with the actual platform administrator account.
2. Open navigation if the sidebar is collapsed.
3. Point out **Your organisation** in the header and the account link.
4. Show the navigation sections and explain that effective permissions control which actions are available.
5. Before an organisation-scoped task, confirm the intended organisation. Use **Mwabu Education Zambia** for the initial platform/global demonstration; select the school context for school projects, AI settings, assessments and teaching workflows.

**Expected result:** an authenticated workspace with the administrator's permitted navigation. Missing links should be investigated as an account/membership/permission/context issue, not bypassed with a different URL.

**Explain:** platform authority and organisation context serve different purposes. A PlatformAdmin administers the platform; an OrganisationAdmin manages its organisation. A parent organisation does not automatically give ordinary staff access to every child school. The backend checks access independently of hidden or visible buttons.

## 4. Establish a school and organisation hierarchy

**Navigation:** **Organisations** → `/app/organisations`.

1. Search for **Mwabu Education Zambia** and open it. Show its type, code, active state and parent relationship.
2. For the prepared sample school, click **Create organisation**.
3. Enter **Organisation name**, a unique **Organisation code**, **Organisation type: School**, and an appropriate **Parent organisation (optional)** if the demonstration needs a hierarchy.
4. Save through the form and open the new organisation profile.
5. Show **People in this organisation**, **Assigned curriculum**, **Guardian relationships**, and the organisation's projects link when available.
6. Open **Edit organisation** to demonstrate editable metadata. Cancel if no change is needed.

**Expected result:** a distinct active school profile with initially empty relationships. Creation of a school does not automatically create teacher accounts, role assignments, curriculum assignments or a project.

**Presenter line:** “The school is a real access boundary. We explicitly connect people, curriculum and programme resources to it.”

**Optional control demonstration:** explain **Deactivate organisation** and its confirmation. Execute only on a disposable sample organisation with no active presentation dependency. Do not deactivate Mwabu Education Zambia or an audience member's real school.

## 5. Create people, attach memberships and assign roles

### A. Platform people directory

**Navigation:** **People** → `/app/users`.

1. Search by name or email. Use **Account status** to distinguish active and inactive accounts.
2. Open a sample account. Show **Account details**, status, last sign-in and **Organisation memberships**.
3. For a new sample identity, click **Create user**, enter email, first/last names and a strong unique **Initial password**. Phone is optional. The current form requires at least 12 characters with uppercase, lowercase, a digit and a symbol.
4. Explain that passwords are not returned after creation and must be shared privately outside the presentation.

**Expected result:** the account exists. A user account alone does not grant access to school functionality; memberships and roles must be assigned.

### B. School membership and delegation

**Navigation:** **Organisations** → sample school → **People in this organisation**.

1. Click **Add existing user**, search/select the sample identity, then **Add membership**.
2. Alternatively, use **Create member account** to create a new account and its membership together. Do not repeat both creation routes for the same email.
3. On the member row, open **Manage roles**.
4. Assign **Teacher** to the teacher, **Learner** to the learner and **HeadTeacher** to the lesson reviewer. Use **OrganisationAdmin** for school administration, rather than granting platform-wide authority.
5. Verify the role appears on the member row. Sign in separately as the teacher/learner and confirm their expected navigation.
6. Return to the administrator's People view to show the same account's organisation memberships.

| Role | Useful demonstration responsibility |
| --- | --- |
| PlatformAdmin | Platform administration, global catalogue oversight and operational review |
| OrganisationAdmin | School/organisation administration within delegated permissions |
| ProjectManager | Organisation-scoped programme/project work |
| HeadTeacher | School oversight and authorised lesson review/publication |
| Teacher | Planning, AI proposals, classroom delivery and permitted assessment management/reporting |
| Learner | Published learning resources and authorised online assessment attempts |
| ParentGuardian | Explicitly linked learners and their assigned curriculum context |
| ContentManager | Global content authoring through an appropriate Platform membership; no automatic platform people administration |
| DataAnalyst | Reporting according to effective permissions and organisation context |

Role names describe intent; inspect the effective permission list in **Manage roles** for the actual grant. Do not claim that a label grants access across every organisation. Assignment options enforce delegation rules; stable permission IDs are not edited through this workflow.

**Optional access-control demonstration:** deactivate/reactivate a disposable membership and show that it affects that organisation. Contrast it with **Deactivate user**, which invalidates account access more broadly. Avoid changing the only platform administrator or the active presenter's membership. Review confirmations before executing a state change.

## 6. Explore curriculum and assign it to a school

**Navigation:** **Curriculum explorer** → `/app/curriculum`.

1. Open **Zambia National Curriculum**.
2. Follow the existing hierarchy: **Versions → Grades → Subjects → Terms → Topics → Competencies → Learning outcomes**. Choose **Grade 4**, **Mathematics**, and the existing **Whole Number** topic where available.
3. Show the managed names and descriptions at the selected outcome. Do not invent a Fractions alignment or official provenance that is absent from these records.
4. Where permissions allow, open the create/edit control for the current level. Explain **Name**, **Code**, **Description**, **Display order** and **Active**. Cancel rather than altering the existing curriculum for show.
5. Return to the sample organisation's **Assigned curriculum** section.
6. Use the curriculum picker to select a **version**, then click **Assign [version name]**. Verify an active assignment appears.

**Expected result:** the school has an explicit curriculum-version relationship, giving its users a starting learning context. Assigning a curriculum does not itself publish resources or grant additional roles.

**Presenter line:** “Teachers choose managed learning outcomes; the underlying hierarchy keeps the plan and resources connected to the same curriculum.”

## 7. Organise resources with tags and collections

**Navigation:** **Tags** → `/app/tags`; **Collections** → `/app/collections`.

1. Browse existing taxonomy before adding duplicates.
2. With global management access, use the available create control to prepare a sample tag such as **Place value** and a collection such as **Grade 4 mathematics presentation**.
3. Enter **Name** and **URL name**; collection forms also expose introductory/display settings.
4. Return to a draft resource's **Tags & collections** section and attach the selected records.
5. Later show the published resource through learner-facing collection/catalogue navigation.

**Expected result:** organisation of the shared catalogue by discoverable relationships. Tags and collections do not replace curriculum mappings or publication checks.

## 8. Content Studio: author, map, review and publish

**Navigation:** **Content studio** → `/app/content`.

### A. Create or inspect a draft

1. Search for the existing **Fractions** draft to show that authoring and published catalogue visibility are different states. Do not publish it without reviewing its actual content, files and rights.
2. For the end-to-end demonstration, create a clearly labelled presentation resource using the create control.
3. Enter **Resource title**, **URL name**, **Short introduction**, **Description / teaching notes**, **Resource type**, **Language**, suggested duration, display order and **Allow downloads** as appropriate.
4. Open the saved detail page. Show **Resource details**, **Resource files**, **Curriculum connections**, and **Tags & collections**.
5. Upload the approved PDF/image/audio/video file through **Resource files**. Confirm the file appears and preview it. Media playback requires an explicit user action; it should not autoplay.
6. Connect it to the correct managed learning outcome using **Curriculum connections**. Add the prepared tag and collection.

**Expected result:** a saved Draft resource with actual files and relationships. An empty metadata record is not a complete teaching resource.

### B. Author an interactive activity

1. Open a draft resource's **Interactive activity authoring** section.
2. Choose one of the implemented formats:

   | Format | What to demonstrate |
   | --- | --- |
   | Multiple-choice practice | Question, answer options, correct answer and explanation |
   | Matching | Match corresponding items and check feedback |
   | Sequencing | Arrange steps in order |
   | Fill in the blank | Enter an accepted answer and review feedback |
   | Number line | Position/select the requested number |
   | Fraction visualisation | Manipulate equal parts and shaded portions |
   | Place-value exercise | Manipulate values in their places |
   | Place-value animation | Play/pause or advance the fixed explanation at a chosen pace |

3. Enter a meaningful **Activity title**, instructions and the fields for the chosen format. For a fractions example use two of four equal parts; for the actual Whole Number alignment, a place-value exercise is more directly aligned.
4. Click **Preview activity** and interact with the result. For question formats, test both correct and incorrect answers and read the explanation. Confirm keyboard operation and readable labels.
5. Click **Save activity file**. Confirm the activity is retained before navigating away.

The renderer uses structured definitions. Do not describe the studio as a general animation editor or a system that executes model-written scripts.

### C. Independent review and publication

1. As the author, click **Submit for review**.
2. Sign in in a separate browser session as an authorised **different reviewer**. An author cannot approve their own activity, even if they hold a platform administrator role.
3. Inspect the metadata, curriculum mapping, all files, activity behaviour, correct answers, explanations, accessibility and reuse rights.
4. For an activity, click **Approve activity** after review. The state becomes **Approved**.
5. An authorised publisher then clicks **Publish resource**. For ordinary non-activity resources, the UI uses **Publish resource** from InReview; the additional Approved stage applies to activity-bearing content.
6. Click **Open published resource** and confirm it is visible through **Learning resources**.

**Expected result:** a real Published resource. Draft/InReview content remains outside the ordinary learner catalogue. Published metadata/files are immutable. **Create revised resource** creates a separate draft; review its files and connections explicitly. Do not assume the old lesson automatically uses that replacement version.

**Optional lifecycle explanation:** **Archive** removes a resource from published catalogue/offline feeds. Do not archive a resource used by the live demonstration. Already exported offline files cannot be remotely recalled.

## 9. AI governance: administrator responsibilities

**Navigation:** choose the intended school/organisation → **Lesson Planner** → **Usage and organisation settings**.

1. Show **Usage, approval and quotas**. Read the actual AI and public-search availability messages.
2. Distinguish server/provider configuration from organisation approval. A server setting being enabled does not prove a reachable provider or an approved organisation.
3. Under **Organisation governance**, inspect:
   - **Organisation approves AI processing of non-personal teaching context**;
   - **Organisation approves public search and secure source retrieval**;
   - **Daily request limit**;
   - **Monthly token limit**;
   - **Monthly estimated cost limit (USD)**;
   - **Extracted evidence / proposal retention (days)**.
4. Explain approval requirements before changing a checkbox. Record the organisation's genuine decision; do not turn approval on merely to make a presentation button work.
5. If an approved change is intended, click **Save organisation settings** and verify the displayed state.
6. Show requests today, charged/reserved tokens, estimated monthly charges and uncertain/estimated usage. Explain that these are quota/accounting estimates, not a provider invoice or a response-time guarantee.

**Expected result:** clear, organisation-specific AI governance. This screen does not provision provider API credentials, install a local model or complete a privacy review automatically.

**Failure handling:** if AI is unavailable, use the saved manual lesson. If a quota is exhausted, show the message and budget information; do not disable controls or inflate limits just to conceal the condition.

## 10. Teacher journey handoff and lesson approval

This part demonstrates what the administrator enables, while preserving the distinction between administrator and teacher work. Use the teacher's account for its private draft. Platform administration is not an impersonation feature, and private lesson ownership/sharing rules still apply.

### A. Teacher planning and an AI action

1. Teacher opens **Lesson Planner** → **Create lesson plan**.
2. Select **Curriculum-aligned**; choose the actual managed outcome. The grade-first discovery option and dependent choices can be shown here. Changing a parent clears dependent selections.
3. Enter title, date, language, duration, class size, objectives, classroom resources and anonymous class-level support needs. Click **Create draft**.
4. Show the structured editor and required teaching sections. The actual database's **Understanding Whole Numbers and Place Value** draft is a useful starting point if accessible to this teacher.
5. Open **Source Explorer** to select relevant published resources. Show source evidence/rights where available. Do not imply that a found public web page is automatically reusable or bundled offline.
6. Open **AI proposals**. Choose an available **AI action**, the **Target section** if needed, and **Instructions for this proposal**. Example: “Using the selected Grade 4 whole-number outcome, improve the introduction with a five-minute place-value demonstration using bottle tops. Keep learner information anonymous.”
7. Read and complete the no-personal-data confirmation only when true. Click **Request AI proposal** once.
8. Show queued/processing/completed state. **Cancel generation** requests cancellation; do not represent it as erasing provider usage already incurred. Continue another presenter segment while waiting rather than repeatedly submitting jobs.
9. Open **Review completed proposal**, inspect **Proposed changes**, explanations, provider/model and usage information. Compare before/after content.
10. Click **Apply reviewed proposal to draft** only after review. The proposal's base revision must still match; a stale proposal must not silently overwrite a newer edit. A proposal does not publish the lesson.
11. Show **Revision history**, saved revision numbers and the reviewed change. If live generation fails, state that honestly and show a previously reviewed proposal or continue manually.

### B. AI-generated content, if global authoring permissions are present

1. In an editable, curriculum-aligned lesson, an authorised global content user can open **Generate interactive content from this lesson**. An ordinary teacher is not automatically a global Content Studio author.
2. Choose **Generate format**: quiz, matching, sequencing, fill blank, number line, fraction, place-value exercise, storyboard, visual explanation, worked example, discussion prompts, activity suggestions or learner explanation, as appropriate.
3. Enter non-personal **Content instructions**, confirm the data statement and click **Queue content proposal**.
4. Review completed structured output and provenance. If an activity proposal is returned, save it to an appropriate draft resource using the displayed control, then complete independent resource approval/publication.
5. Treat storyboards as reviewed plans/captions/visual descriptions. They are not automatically rendered animation assets or executable scripts.

### C. Review the completed lesson

1. Teacher completes **objectives**, **starter**, **development**, **formative assessment** and **conclusion**; ensure activity minutes total the planned duration.
2. In **Review & sharing**, enter a **Review / lifecycle note** and click **Submit lesson for review**.
3. As a separate authorised reviewer, find the submitted lesson in the same organisation. Review curriculum alignment, pedagogy, timings, media, teaching steps and suitability of any AI changes.
4. Use **Return lesson to draft** for corrections, or **Approve and publish lesson** when justified. Optionally select **Publish as reusable organisation template** if intended and authorised.
5. Verify Published status and the new revision. Published lessons are immutable; duplicate to create a new private draft for later changes.

Read-only colleague sharing is available through **Explicit read-only sharing** to authorised colleagues. Sharing is not edit delegation or public publication.

## 11. Show classroom content delivery

**Navigation:** teacher's lesson plan → **Prepare Lesson Journey** → **Teach Lesson**.

1. In preparation, show learner objectives, the teaching-step timeline and planned minutes.
2. Select a step. Contrast **Teacher instructions (private)** / **Teacher notes (private)** with **Learner-facing explanation**, controlled hint and answer.
3. Under **Linked approved resources**, preview the exact published attachment. Use **Search approved resources** to locate and attach suitable published files to that specific step. Save using **Save preparation** and verify the revision changes.
4. Enter **Teach Lesson**. Verify the learner-facing prompt is visible and private guidance is absent.
5. Use **Next step**, **Previous step** and **Lesson overview**. Select a resource through **Teaching resource**.
6. Manipulate the activity; use **Reveal activity answer**, then **Show explanation**, and **Reset demonstration**. Step-level **Show hint** and **Reveal answer** are separate teacher controls.
7. Show timer/fullscreen controls as useful classroom tools; check the actual display rather than assuming mirroring/fullscreen works on every device.
8. Reach the recap and click **Finish lesson** to return to the library.

**Expected result:** a complete saved teacher-led journey. Whole-class interaction records no learner scores. It must not be presented as an authenticated assessment attempt.

## 12. Learning resources, assessments and progress

### Published resource discovery

**Navigation:** **Learning resources** → `/app/resources`; **Collections** → `/app/collections`.

1. Search/filter by available curriculum context, resource type, language, tag and collection.
2. Open the published resource, read its description/context, preview files and demonstrate allowed downloads.
3. Sign in separately as the learner and show the same published resource. Draft resource IDs should not provide a learner with authoring access.
4. Distinguish temporary resource practice feedback from saved assessment results. Resource visits/recent items are not proof of attainment.

### Create and take an online assessment

1. With assessment management permission in the intended organisation, open a published supported interactive resource.
2. Click **Use as an assessment**. Verify **Added to organisation question bank** and follow **Open learning assessments**.
3. Sign in as the learner in the same school. Open **Learning assessments** → `/app/assessments` → **Choose an assessment**.
4. Submit an example response and inspect the returned result and attempt history. The server marks against the pinned published resource version; classroom/practice answers are not reused as authoritative results.
5. Return as an authorised teacher/reporting user, choose the bank and show its learner summary: attempts, best score and last submission information where available.
6. Explain idempotent submission handling, attempt history and deletion/retention. The current interface states that results expire after 365 days and learners can delete their own results sooner. Do not delete actual learner results during a presentation.

**Current prerequisite:** the actual database has no learner role membership or question bank yet. Prepare them through the preceding account and content workflows. Empty assessments must not be presented as completed progress tracking.

## 13. Projects and guardian relationships

### Projects

**Navigation:** intended organisation → **Projects** → `/app/projects`.

1. Click **Create project**. Enter **Project name**, unique **Project code**, **Programme description**, **Status**, dates and optional curriculum context.
2. Explain the lifecycle options **Planned**, **Active**, **Completed**, **Archived**.
3. On the saved detail page, demonstrate assignment pickers for **Schools & sites**, **Project participants** and **Assigned learning resources**.
4. Choose the appropriate authorised school, actual memberships and published resources. Verify each assignment appears under the correct category.
5. Show how **Edit project** changes programme metadata. Explain that removing a project assignment does not delete the underlying person, school or content.

**Expected result:** a real programme structure connecting organisations, people and approved learning resources. Project ownership does not automatically confer management authority in every assigned school.

### Guardian links

**Navigation:** school organisation → **Guardian relationships**.

1. Ensure active ParentGuardian and Learner memberships exist in that same organisation.
2. Click **Link guardian & learner**, choose **Guardian** and **Learner**, and save.
3. Verify the relationship. In a separate guardian session, open **My learners** and show only the explicitly linked learner and curriculum context.
4. Explain active-state controls without changing a real relationship. This is not a complete parent attainment dashboard or permission to view unrelated learners.

## 14. Offline delivery and device support

### Complete lesson package

1. Open a **Published**, accessible lesson at its current revision.
2. Expand **Complete offline lesson package**. Inspect the lesson for personal data and check the secure-storage confirmation only when true.
3. Click **Download complete lesson package**. Wait for **Verified package exported**. This differs from the simpler **Download offline copy** lesson-text export.
4. Open the exported file normally, disconnect connectivity and test its actual steps and activity controls before presentation. Keep an online/screenshot/paper fallback.
5. Explain SHA-256 verification, completed-chunk download recovery, version-aware rechecking and **Clear temporary downloaded media**. Current package limits are 30 assets / 25 MiB; temporary media storage is bounded at 50 MiB.

**Known browser limitation from the separate demo rehearsal:** interactive offline playback was presenter-confirmed, but Chrome blocked an inline PDF pane. A separate checksum-verified PDF was supplied for that demo. Do not claim the current application's future package or its PDF viewer has been tested merely because the earlier demo package worked. Rehearse the actual exported file; use a separate approved PDF/printed guide if needed. Do not weaken sandbox/CSP settings as a presentation shortcut.

**Privacy boundary:** learner-identifiable offline progress collection and synchronisation are disabled pending explicit privacy, device-security and conflict-policy approvals. Offline packages contain teaching content, not a learner outbox. Do not claim offline assessment answers will later synchronise.

### Device oversight

**Navigation:** **Devices** → `/app/devices`, in the intended organisation.

1. Show registered device name, platform, active/revoked state, last activity and credential expiry, if real devices exist.
2. Show **Catalogue checkpoints** and explain acknowledged catalogue positions. They are not learner scores or engagement/attainment measures.
3. Explain **Revoke access** and its confirmation. Use it only on an explicitly disposable test device; revocation requires a new registration before synchronising.
4. If there are no devices, show **No registered devices** honestly. This browser does not register a pretend device or simulate synchronisation. Device credential provisioning/rotation is a separate paired-device/API workflow; secrets are not displayed here.

## 15. Reports, audit and account security

### Reports

**Navigation:** **Reports** → `/app/reports`.

1. Read whether the page says **Platform-wide indicators** or organisation indicators. For PlatformAdmin, the current Reports implementation selects the platform-wide report; changing the organisation selector is not proof that this account has switched to an organisation-only report.
2. Show generated time and the actual counts: people, active memberships, organisations, schools, projects, active curricula, catalogue resources, mapped resources, active/revoked devices and saved sync checkpoints.
3. Show **Publication states** and **Resource types**.
4. Explain that these are operational aggregates, not a historical analytics snapshot or learner achievement dashboard. Use the assessment summary for authorised online learner results.

### Audit trail

**Navigation:** **Audit trail** → `/app/audit`.

1. Show **An identifier-only audit trail** and its default last-seven-days scope.
2. Use **Event type**, **From**, **To**, then **Apply filters**. Use a recorded event type from the actual list; `content.published` is the form's example, not a guarantee that an event exists.
3. Show When, Event, Resource, Actor/organisation and Support reference. Relate a recorded event to an action completed during preparation if present.
4. Explain that identifiers/correlation references support investigation; passwords, tokens and personal-field snapshots are deliberately absent. This is not an unrestricted database change-history viewer.

### Your account

**Navigation:** **Your account** → `/app/account`.

1. Show profile and **Your organisations**.
2. Explain **Change password** and its Current/New password fields. Do not change the presenter's credentials while screen-sharing.
3. Explain **Sign out everywhere** and its confirmation. Execute it only as the closing action when ready to terminate all of that account's sessions.
4. Otherwise finish with the ordinary **Sign out** control.

## 16. Completion checklist and honest closing

Record which items were actually demonstrated; blank means not yet rehearsed.

- [ ] Existing actual PlatformAdmin account authenticated; correct organisation confirmed.
- [ ] Sample school created/opened; memberships and scoped roles verified.
- [ ] Teacher and learner login checked in separate sessions.
- [ ] Managed outcome explored and curriculum version assigned to school.
- [ ] Resource files, mappings, tags and collection verified.
- [ ] Activity previewed/saved; different reviewer approved; resource published.
- [ ] Published resource visible to learner; draft remains restricted.
- [ ] Organisation AI state/quotas checked; provider and proposal actually exercised, or unavailable state explained.
- [ ] Teacher reviewed/applied a proposal and saved a revision, if live AI was demonstrated.
- [ ] Separate reviewer approved/published the completed lesson.
- [ ] Prepare → linked content → Save → Teach → activity → controlled reveal → recap → Finish completed.
- [ ] Question bank, learner submission and authorised performance summary verified.
- [ ] Project/guardian examples shown, or honest empty states identified.
- [ ] Actual exported offline lesson tested; PDF/browser limitations noted.
- [ ] Reports scope, device state and audit evidence explained accurately.
- [ ] Presentation display checked; credential/configuration windows closed; session ended appropriately.

**Suggested closing:** “The administrator connects schools, users and curriculum, governs AI and publishing, and monitors operations. Teachers remain responsible for lesson judgement and classroom delivery. Learners use approved resources and authorised online assessments. Offline teaching content is supported; identifiable offline learner progress remains gated.”

## 17. Troubleshooting during the walkthrough

| Symptom | Action |
| --- | --- |
| Earlier demo credentials fail | Use actual application accounts; databases are separate. Do not restart the isolated demo launcher. |
| Missing school or navigation/action | Verify active account, selected organisation, membership and effective permissions. Use the correct account/session; do not grant PlatformAdmin as a workaround. |
| Resource missing from learner catalogue | Check Published state, active visibility/mapping and the current filters. A Draft Fractions record is not published learning content. |
| Private teacher lesson not visible to administrator | Use the owning teacher, explicit read-only sharing or normal submitted-review access. Do not assume platform administration is lesson impersonation. |
| AI unavailable or slow | Show the actual availability/error/job state, check approval/provider/budgets privately, then use a saved reviewed lesson. Do not resubmit repeatedly or claim live AI for static content. |
| Activity self-approval denied | Switch to a different authorised reviewer; this is the intended governance boundary. |
| Proposal apply/revision conflict | Reload the latest saved revision, compare changes and request/review an up-to-date proposal. Do not overwrite another saved revision. |
| Offline export refused | Confirm current published lesson, downloadable published attachments, supported formats, package limits and absence of unbundleable external references. Do not suppress integrity/access errors. |
| Inline offline PDF blocked | Use the separately approved PDF in a normal reader or printed guide; keep interactive activity playback separate. |
| No reports/assessment/device data | Explain the current empty state and preparation prerequisites; do not invent activity or attainment. |

## 18. Evidence and scope of this guide

Navigation, form/action labels and limitations were checked against the current frontend routes/components and supporting backend/documentation. The current actual database inventory was read-only; application health was checked. No users, roles, organisations, resources, AI approvals or lesson content were changed to write this guide. No administrator password was accessed or reset, and no new live authenticated administrator walkthrough was performed.

The earlier end-to-end lesson rehearsal and its screenshots are documented in `docs/demo-readiness-2026-10-20.md`; they refer to the **separate demo environment**, not proof of current actual-database content or readiness for every role. That earlier document's running-process statements are historical: the demo was subsequently stopped and the actual database-backed application started.

Implementation references: `web/src/app/Shell.tsx`, `web/src/main.tsx`, `web/src/features/organisations/Organisations.tsx`, `users/Users.tsx`, `curriculum/Curriculum.tsx`, `content/Management.tsx`, `content/activities/ActivityComposer.tsx`, `lessons/PlannerSettings.tsx`, `lessons/LessonEditor.tsx`, `lessons/ContentAi.tsx`, `content/Assessments.tsx`, `projects/Projects.tsx`, `devices/Devices.tsx`, `reports/Reports.tsx`, `reports/Audit.tsx`, and `docs/sprint-2-privacy-and-offline.md`.

