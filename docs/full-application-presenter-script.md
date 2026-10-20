# Mwabu Learn — word-for-word application demonstration

Prepared 20 October 2026 for the actual local application at **https://localhost:7042/**.

**How to use this script:** read the blockquoted paragraphs aloud. Instructions in square brackets are actions for the presenter; do not read them aloud. When an action fails or its data is missing, use the labelled fallback paragraph rather than describing it as successful.

Allow **45–60 minutes**, including account changes and discussion. A 20-minute route is provided at the end. The complete script covers all nine implemented roles, including repeated teacher/administrator visits where the workflow requires them. It demonstrates implemented functional areas; it is not a claim that every workflow below has already been exercised in the current database.

## Before presenting — do not read aloud

### Current application facts

A read-only check while preparing this script found:

| Area | Actual current state |
| --- | --- |
| Organisation | Mwabu Education Zambia — Platform type; no separate School organisation yet |
| Assigned roles | PlatformAdmin, OrganisationAdmin, ContentManager, Learner, ParentGuardian and Teacher; two Teacher memberships |
| Roles still to prepare | HeadTeacher, ProjectManager and DataAnalyst memberships/accounts |
| Resources | **Fractions — Published**; **Interactive Test — Approved**; **Test — Draft** |
| Lessons | **Understanding Whole Numbers and Place Value**, **Division**, **Multiplication** — all Draft |
| Project | One existing project |
| Question banks | None |
| Guardian relationships | None |

These records belong to the actual `mwabu_learn` database. The earlier `demo.*@mwabu.invalid` credentials and exported fractions demonstration belong to a different database and must not be presented as actual-application data.

### Preparation required for the full route

1. Privately verify login credentials for all nine roles. Provision missing accounts/memberships through normal administrator controls; do not grant everyone PlatformAdmin. No passwords belong in this script.
2. For a realistic school demonstration, create a clearly labelled **Presentation School** and give the teacher, head teacher, learner, guardian and organisation administrator appropriate active memberships there. Decide the owning organisation and permissions for the project manager and analyst. Retain the content author's appropriate Platform membership.
3. Assign the intended curriculum version to that school. Use the actual managed Grade 4 Mathematics outcome that fits the material; do not claim the Fractions resource is curriculum-aligned merely because its title says Fractions.
4. Inspect the published **Fractions** resource's file, licence, content and mappings. Confirm it is suitable before using it in the presentation.
5. Inspect **Interactive Test**. It is Approved, not Published. An authorised publisher must publish it normally before learner use or question-bank creation. Approval status alone is not publication.
6. Use an existing draft lesson owned by the presenting teacher, or create a clearly labelled presentation draft. Complete the objectives, starter, development, formative assessment and conclusion; ensure timings total the duration. Prepare learner-facing steps, a controlled hint/answer, a recap and exact published attachments.
7. Keep a draft copy for live edits. Use a separate independently reviewed Published lesson for complete offline export. No lesson was Published at the inventory check.
8. Add a published supported interactive resource to the school question bank. Prepare an authorised learner to make an example submission. Use synthetic answers; do not expose real learner records on the projector.
9. Create an explicit guardian–learner link between suitable active memberships in the same school if demonstrating the parent journey.
10. Verify organisation AI approval, provider availability, quota and a successful request beforehand. Keep a saved reviewed lesson/proposal ready. Do not enable privacy approvals merely for a demonstration.
11. Use separate browser profiles/sessions for roles, or sign out and sign in sequentially. Ordinary tabs share cookies and are not separate role sessions. Refresh once before starting so the latest local fixes load.
12. Check the physical display, zoom and fullscreen. Hide credentials/configuration windows. Keep screenshots, a separate PDF/printed guide and paper manipulatives available.

The instructions below do not authorise overwriting real material or turning off security controls. Use clearly labelled presentation records for live changes. Do not deactivate the current administrator, archive a resource needed later, or delete learner results during the tour.

## 1. Opening — approximately 1 minute

[Show the sign-in page. Do not project password entry.]

> “Good morning, and thank you for joining us. Today I will demonstrate Mwabu Learn from the perspective of the people who operate it, prepare learning, teach and learn with it.
>
> “We will follow a complete workflow: establishing access, connecting curriculum, preparing and approving resources, planning with AI assistance, delivering a lesson, and reviewing an online assessment.
>
> “I will change accounts as we move between roles. This lets us see what each person is actually allowed to do. I will also show where offline teaching works and where the platform deliberately places limits on collecting learner information.”

## 2. Platform administrator — access and organisation setup

[Sign in as the actual PlatformAdmin. Open **Your workspace**. Show the navigation and **Your organisation** selector.]

> “I am starting as the platform administrator. This role oversees organisations, people, the shared curriculum and content catalogue, and operational information.
>
> “The organisation shown here is the context for organisation-specific work. Permissions still matter: holding a school role does not automatically grant access to every school or to global content administration.”

[Open **Organisations**. Show **Mwabu Education Zambia**, then the prepared Presentation School. If preparing live, use **Create organisation**, enter a unique code, select School and save.]

> “Here we organise the institutions using the platform. Each organisation has a name, type, active state and, where appropriate, a parent organisation.
>
> “A school is an access boundary. We explicitly connect its staff, learners, curriculum and programme resources rather than assuming those relationships.”

[Open the school profile. Show **People in this organisation**, **Assigned curriculum** and **Guardian relationships**.]

> “This profile brings those relationships together. Creating a school does not automatically create its staff accounts, assign its curriculum or publish its learning resources. We will connect those pieces through their normal workflows.”

[If no school was prepared, read this instead of claiming one exists.]

> “This installation currently has the platform organisation. A separate school must be created and populated before I can demonstrate school-specific relationships. I will show the available controls without presenting an unconfigured school as operational.”

### People, memberships and roles

[Open **People**, search for a prepared sample person, and open their account.]

> “An account identifies the person. Their memberships connect them to organisations, and their roles determine what they can do in each context.
>
> “The People directory lets an authorised platform administrator find accounts, inspect active state and see organisation memberships. Passwords are not displayed here.”

[Show **Create user**, then cancel unless a new sample identity is intentionally being provisioned. Return to the sample school. Show **Create member account**, **Add existing user** and **Manage roles**.]

> “We can create a new account, add an existing person to an organisation, or create a new member account and membership together.
>
> “Here I can assign the appropriate role: for example Teacher, Learner or HeadTeacher. I do not need to make every user an administrator. Delegation is controlled by the permissions of the person making the assignment.”

[Show membership and account active-state controls; do not deactivate a real account.]

> “Account status and membership status are separate controls. A membership change concerns that organisation; disabling an account affects access more broadly. These controls have real consequences, so I will not execute a disruptive change during this presentation.”

### Curriculum and discovery structure

[Open **Curriculum explorer**. Select Zambia National Curriculum and the actual version, Grade 4, Mathematics, term, topic, competency and outcome.]

> “This is the managed curriculum structure. We move from a curriculum version through grade, subject, term and topic to competencies and learning outcomes.
>
> “The objective is to connect plans and resources to a shared learning context. A resource title alone does not establish curriculum alignment; the explicit relationship matters.”

[Show a create/edit control without modifying the existing curriculum. Return to the school's **Assigned curriculum** and show the version assignment.]

> “Authorised curriculum managers can maintain names, descriptions, ordering and active states. Schools are assigned curriculum versions explicitly, so the learning context can be traced rather than inferred from typed text.”

## 3. Organisation administrator — school operations

[Switch to OrganisationAdmin in Presentation School. Open **School & organisation**.]

> “I am now the organisation administrator. This role manages the school's permitted work without needing platform-wide people administration.
>
> “I can inspect the school profile, its people and their roles within my delegated permissions. This is where a school maintains its operational relationships.”

[Show member search/filter, **Create member account**, and the roles dialog for a disposable sample membership.]

> “I can provision a new member where my permissions allow it and delegate roles that I am authorised to grant. I cannot use school administration as a way to browse unrelated accounts or give myself broader platform authority.”

[Show assigned curriculum and guardian relationships.]

> “The assigned curriculum gives the school a managed learning context. The guardian relationship explicitly connects the right parent or guardian to the right learner. We will see the guardian's restricted view later.”

## 4. Content manager — resources and interactive authoring

[Switch to ContentManager with appropriate Platform membership. Open **Content studio**.]

> “I am now working as a content manager. This is the authoring workspace for teaching and learning resources.
>
> “Authoring is separate from learner access. A draft can be prepared and checked here without becoming visible in the published learning catalogue.”

[Open the prepared resource. Show its metadata form, including Resource type and Language dropdowns.]

> “Resource type and language are selected from readable lists. Authors should not have to remember internal codes to describe the material.
>
> “The resource also has a title, introduction, teaching description, suggested duration and download setting. We record its authorship and reuse information so the publication decision is informed.”

[Show **Resource files**, preview the approved file, then **Curriculum connections** and **Tags & collections**.]

> “The teaching file is attached to the resource. Here we check that the file actually opens, that its language and content are suitable, and that its curriculum connection is correct.
>
> “Tags and collections make published material easier to find. They complement curriculum mapping; they do not replace it.”

### Interactive activity

[Open the prepared activity draft or an editable presentation copy. Expand **Interactive activity authoring**.]

> “Resources can include structured interactive activities. The available formats include multiple-choice practice, matching, sequencing, fill-in-the-blank tasks, number lines, fraction visualisations and place-value exercises. There is also a fixed place-value explanation with playback controls.”

[Select the prepared format. Show instructions, correct answer and explanation. Click **Preview activity**. Try an incorrect answer, then a correct answer; show feedback. Click **Save activity file** if intentionally editing.]

> “The author prepares the task and its feedback, then previews the interaction. I am checking both a correct and an incorrect response because useful feedback matters as much as the question itself.
>
> “These activities use defined structures and a controlled renderer. We are not executing scripts written by an AI model.”

### Approval boundary

[For a Draft activity, click **Submit for review**. If using current Interactive Test, show its Approved state instead.]

> “Once the content is ready, it moves into review. The reviewer must check curriculum accuracy, answers, explanations, language, accessibility, rights and the absence of personal information.
>
> “An activity author cannot approve their own work, even if they also hold a powerful administrator role.”

[Switch to a different authorised reviewer. Inspect the activity and use **Approve activity** only after actual review. Use the authorised **Publish resource** action. For Interactive Test already Approved, the remaining step is publication after confirming that review remains valid.]

> “Approval and publication are separate states for activity content. Approved means reviewed; Published makes the resource available through the learner catalogue.
>
> “Published metadata and files become immutable. A revision creates a new draft resource, which must carry the appropriate reviewed files and connections before it is published.”

[Click **Open published resource** and verify the resource is available.]

> “We can now see the approved material through its published resource page. This is the version teachers and learners can use.”

[If publication was not completed, read this instead.]

> “This resource has not reached Published status, so I will not present it as learner-ready. I will continue with material that is already published.”

## 5. Platform/organisation governance — enabling AI responsibly

[Use an authorised account in the intended teaching organisation. Open **Lesson Planner** → **Usage and organisation settings**.]

> “Before showing an AI request, I want to show who controls its availability.
>
> “AI requires a configured, available provider, organisation approval and available budgets. These settings make that decision explicit rather than treating AI as an unrestricted background service.”

[Show approval state, request/token/cost limits, retention and current usage. Do not toggle approval without a real authorised decision.]

> “The organisation controls approval for non-personal teaching context and, separately, public search and source retrieval. We can also inspect request limits, reserved or charged tokens, estimated cost and evidence-retention settings.
>
> “The usage figures are estimates for operational control, not a provider invoice. Learner-identifiable information should not be included in teaching prompts.”

## 6. Teacher — manual planning, sources and AI proposals

[Switch to Teacher. Open **Lesson Planner**. Choose the teacher-owned prepared draft, or **Create lesson plan**.]

> “I am now the teacher. The teacher journey begins with a learning intention and a classroom context, rather than with an AI prompt.
>
> “I can plan manually, work from a managed curriculum outcome, discover resources and request assistance when appropriate.”

[If creating, choose **Curriculum-aligned**, use grade-first/dependent discovery, and complete the actual outcome path. Show Free-form as an alternative without changing the saved plan unintentionally.]

> “Here I choose the grade and follow the subject and topic to an existing learning outcome. Dependent choices keep the context connected. Free-form planning is also available when a managed curriculum alignment is not appropriate.”

[Show title, duration, date, language, class size, objectives, resources and anonymous support needs.]

> “These details describe the whole class and the teaching conditions. I can record available materials, learning objectives and support needs without putting individual learner identities into the plan.”

[Show **Structured editor**, required sections, minutes and the Ministry lesson plan form/progression section where present.]

> “The lesson is an editable teaching document. I can develop the introduction, teaching sequence, learner activities, assessment and conclusion, and check that the activity time matches the planned duration.
>
> “The additional lesson-form fields support the teacher's planning and progression record. They remain something the teacher reviews, rather than something we assume is correct because it was generated.”

### Resource grounding

[Open **Source Explorer**. Select a suitable published local resource. Show selected evidence/citations and source controls. Only use public web retrieval if configured, approved and appropriate.]

> “I can connect the plan to teaching resources and inspect the evidence used. A search result is not automatically reliable or licensed for reuse. The teacher still checks suitability, rights and relevance.
>
> “For this demonstration, I will prefer the published resources we have already inspected.”

### Live AI request

[Open **AI proposals**. Confirm the displayed availability. Select a supported **AI action** and **Target section** where needed.]

> “The AI works through proposals. I choose the action and the part of the lesson I want help with; the result does not silently replace the teacher's work.”

[For a Whole Number lesson, enter: “Improve the introduction with a five-minute Grade 4 place-value demonstration using bottle tops. Keep the selected learning outcome and avoid personal learner information.” Adapt this to the actual selected outcome. Confirm the no-personal-data statement, then click **Request AI proposal** once.]

> “I am requesting a practical change that fits the selected outcome and classroom resources. The request is handled as a durable job, so I can see its state and continue other work while it is processed.”

[Show job state. While waiting, explain cancellation without repeatedly submitting.]

> “The job state tells me whether the request is queued, processing, complete or unsuccessful. Cancellation requests are available, but I do not assume cancellation removes usage that has already occurred.”

[On completion click **Review completed proposal**. Show proposed changes, explanation, provider/model and usage.]

> “This is the proposal. I can compare it with the existing lesson, inspect the explanation and check whether the teaching remains accurate and appropriate.
>
> “I decide whether to apply it. The proposal is tied to the saved lesson revision so that an older response cannot silently overwrite newer work.”

[Click **Apply reviewed proposal to draft** only after actual review. Show saved revision and **Revision history**.]

> “I have applied the reviewed change to the draft. The revision history gives us a record of the lesson's changes. Applying an AI proposal does not publish the lesson.”

[Optional: show another available action or section-targeted request, without waiting for a second live generation.]

> “The available actions also support focused changes to existing work. For example, I can target a section rather than regenerate everything. The same review and revision safeguards apply.”

[If AI is unavailable, slow or fails, use this paragraph instead of claiming a result.]

> “The live AI request is not available within this demonstration window. The manual editor and saved lesson remain usable. I will continue with the prepared teaching content and, if available, show a previously reviewed proposal clearly labelled as an earlier result.”

### Sharing, print and export

[Show **Review & sharing**, explicit read-only sharing where the teacher owns the lesson, **Print preview**, **Export saved PDF**, and **Duplicate lesson**. Do not expose private teacher notes as learner content.]

> “The teacher can share read-only access with authorised colleagues, inspect a print preview and export the saved lesson. A duplicate gives us a new editable draft while preserving the published version.
>
> “The teacher's planning record and the learner-facing teaching view are separate.”

## 7. Head teacher — lesson review and publication

[As Teacher, complete the required sections and timings, enter a lifecycle note and click **Submit lesson for review**. Switch to a different authorised HeadTeacher in the same school.]

> “I am now the head teacher reviewing a submitted lesson. Review access is part of this role's delegated responsibilities; it is not unrestricted access to every private teacher draft.
>
> “I check the learning outcome, objectives, sequence, timing, inclusion support, formative assessment, resource suitability and any AI-assisted changes.”

[Show the submitted plan and review note. Point out **Return lesson to draft** and **Approve and publish lesson**.]

> “If changes are needed, I return the lesson to draft with a clear note. If it is ready and I hold publication permission, I can approve and publish it. An organisation template is an explicit publication choice, not the default for every private plan.”

[Publish only after genuine review. Optionally select **Publish as reusable organisation template** if intended. Verify Published status and revision.]

> “The approved version is now published. It cannot be edited in place. Further development starts with a new draft, while the reviewed version remains identifiable.”

[If HeadTeacher has not been provisioned or publication is not completed, read this alternative.]

> “A separate authorised lesson reviewer has not yet completed this step in this installation. I can demonstrate preparation and teaching from the teacher's draft, but I will not describe it as published or demonstrate complete offline export from it.”

## 8. Teacher — preparation and classroom delivery

[Return as Teacher. For live editing use the prepared Draft copy. Open **Prepare Lesson Journey**.]

> “We are back with the teacher, preparing the actual classroom sequence.
>
> “This timeline turns the plan into teaching steps. Each step has a purpose, a duration, learner-facing text and the resources needed at that moment.”

[Show private instructions and learner text. Select a step, preview its linked approved resource and demonstrate **Search approved resources**. Make a small intentional edit to a draft note and click **Save preparation**.]

> “These instructions and notes are private preparation. This explanation is what the class will see. I can attach the approved resource to the correct step and save the preparation as a new revision.”

[Confirm saved revision. Click **Teach Lesson**.]

> “I am entering the learner-facing teaching view. The private preparation notes are not displayed here.
>
> “I can move through the sequence, use the overview, manage the timer and enter fullscreen where the display supports it.”

[Read the prepared introduction. Advance to the interactive step and select the attachment from **Teaching resource**. Interact with the activity.]

> “Here the class can explore the concept together. This is a teacher-led demonstration, so the interaction is not being recorded as an individual learner score.”

[For the prepared fraction visual: click two of four parts. For place value: manipulate the appropriate published exercise. Use the correct paragraph.]

> “In this fraction example, two of four equal parts are shaded. I can ask learners to explain the amount before revealing the answer.”

> “In this place-value example, we can manipulate the values and discuss what each place contributes to the whole number.”

[Click **Reveal activity answer**, then **Show explanation**. Demonstrate **Reset demonstration**. At a question step, use **Show hint** and the separate step-level **Reveal answer**.]

> “The teacher controls the reveal. I can offer a hint, allow discussion, then show the answer and explanation. Resetting lets another group explore without retaining the previous demonstration state.”

[Advance through the remaining prepared steps to recap, then click **Finish lesson**.]

> “We close by returning to the learning objective and asking learners to explain the key idea. Finish takes us back to the lesson library, ready for the next teaching task.”

## 9. Learner — resources, practice and online assessment

[Switch to Learner in the intended school. Open **Learning resources**.]

> “I am now signed in as a learner. The learner sees approved published learning material, rather than the author's drafts or administrative tools.”

[Search/filter the catalogue using available curriculum context, type or collection. Open the published Fractions resource or the prepared aligned resource. Show files and permitted downloads.]

> “The learner can discover resources, open the teaching material and use permitted downloads. The catalogue relationships help connect the resource to its learning context.”

[Open the published interactive activity and demonstrate practice feedback.]

> “This resource practice gives immediate feedback. It is separate from an organisation's saved online assessment attempts. Completing practice is not the same as submitting an assessed result.”

### Saved online assessment

[Before this segment, an authorised teacher/admin must open a supported published activity, click **Use as an assessment** and verify it is added to the organisation question bank. As Learner open **Learning assessments**, choose the bank, answer and submit.]

> “This is an online assessment made available by the organisation. The server marks the submission against the pinned published resource version and returns the result.”

[Show returned marking and **Your attempt history**. Do not delete actual results.]

> “The learner can see the result and attempt history. The interface also supports deletion of their own results. The current retention period shown here is 365 days.
>
> “The submission has an idempotency key so retrying an unchanged request can be handled without creating an unintended duplicate attempt.”

[If no bank has been prepared, use this instead.]

> “No assessment has been assigned to this organisation yet. This empty state is intentional. The published interactive resource must first be added to the question bank before I can demonstrate a saved learner submission.”

[Switch briefly to authorised Teacher and show **Teacher performance summary** for the selected bank, if a submission exists.]

> “The teacher can now inspect the authorised organisation summary, including attempts, best result and last submission. We do not infer attainment from page visits or from whole-class demonstrations.”

## 10. Parent or guardian — explicitly linked learners

[Switch to ParentGuardian. Open **My learners**. Use an actual prepared same-school guardian link.]

> “I am now the parent or guardian. This view is based on an explicit relationship to a learner, not a general ability to browse the school's children.
>
> “I can see the linked learner and assigned curriculum context. I cannot use this relationship to view unrelated learners or administrative records.”

[Show the available linked context. Do not claim an attainment dashboard if none is displayed.]

> “This is the linked learner context currently available. I will not describe it as a complete parent progress dashboard, because resource access and curriculum context are different from assessed attainment.”

[If no relationship exists, read this alternative.]

> “This account has no linked learner yet. A school administrator must create the explicit guardian relationship before learner context appears here.”

## 11. Project manager — programmes, sites and resources

[Switch to ProjectManager in the appropriate owning organisation. Open **Projects** and the prepared existing project.]

> “I am now looking at a programme through the project manager role.
>
> “A project has an owning organisation, a lifecycle, dates and optional curriculum context. It connects schools or sites, member participants and published learning resources.”

[Show **Schools & sites**, **Project participants**, **Assigned learning resources**. Open **Edit project**, explain lifecycle and cancel unless intentionally updating a sample.]

> “These are explicit assignments. A programme can identify which sites and participants are involved and which resources it uses.
>
> “The lifecycle distinguishes planned, active, completed and archived work. Owning a project does not automatically grant management rights over every assigned school.”

[Show an assignment picker. Add/remove only a deliberate disposable sample assignment if needed.]

> “Assignment changes update the programme relationship; they do not delete the underlying user, school or resource.”

[If this role/membership is not prepared, explain rather than showing an administrator as if it were ProjectManager.]

> “The project-management capability is implemented, but a ProjectManager membership has not yet been prepared for this installation. I will not represent this administrator session as a project-manager permission test.”

## 12. Data analyst — operational reporting

[Switch to DataAnalyst in the intended reporting organisation. Open **Reports**. Read the scope actually displayed.]

> “I am now the analyst. The first thing I check is the scope of the report: whether it represents this organisation or the wider platform.
>
> “These figures are actual operational aggregates. They help us understand deployment and catalogue coverage; they are not a substitute for a learner assessment report.”

[Show generated time, available counts, **Publication states** and **Resource types**.]

> “We can inspect people and active memberships, organisations and schools, projects, curriculum and resource coverage, and available device or checkpoint information according to this account's permissions.
>
> “A count of visits, resources or synchronisation checkpoints does not prove that learning has occurred.”

[If DataAnalyst is not provisioned, use the platform administrator Reports screen but explicitly say the following.]

> “This screen is currently being shown under platform administration, not a prepared DataAnalyst account. The displayed scope is platform-wide, so this is not evidence of organisation-only analyst access.”

## 13. AI-assisted Content Studio — optional extension

[Use an account with both the required lesson access and global Content Studio permissions. Open an editable curriculum-aligned lesson → **Generate interactive content from this lesson**.]

> “AI assistance can also propose structured learning content from an aligned lesson. This requires the relevant organisation approval, budgets and global content-authoring permissions. It is not automatically available to every teacher.”

[Show **Generate format** options, **Content instructions**, no-personal-data confirmation and **Queue content proposal**. If pre-rehearsed, submit once and inspect a completed output.]

> “The available proposals include question formats, mathematics activities, learner explanations, discussion prompts, worked examples and animation storyboards.
>
> “We inspect the proposal and its provider/model provenance. Saving an activity adds draft content; it still needs independent review and publication.”

[For a storyboard, show captions/visual descriptions and timing only if available.]

> “A storyboard is a structured plan for an animation. It is not a claim that a finished animated media file has been rendered, and the platform does not execute model-generated scripts.”

## 14. Offline teaching and device oversight

[Open the prepared Published lesson at its current revision. Expand **Complete offline lesson package**.]

> “Offline teaching is useful where connectivity is unreliable. This export packages an accessible published lesson and supported approved downloadable attachments.
>
> “Before exporting, the teacher checks the teaching content for personal information and confirms secure handling of the file.”

[Complete the confirmation only when true. Click **Download complete lesson package**, wait for **Verified package exported**, then open the actual file normally and disconnect for the rehearsed demonstration.]

> “The download verifies the files and checks the package version. Completed download chunks can be recovered on retry, within the temporary storage limits.
>
> “This exported teaching file can operate without the live application, but it is a copy. It cannot receive a remote revocation, so we must recheck current versions and handle obsolete copies appropriately.”

[Demonstrate only the actual offline steps/activity already checked. If the PDF is blocked, open the separately approved PDF/printed guide instead.]

> “The interactive teaching content is available here. If the browser blocks its inline PDF viewer, I use the separate approved document or printed guide; I do not turn off browser security to force it to display.”

> “Learner-identifiable offline progress collection and synchronisation are deliberately disabled pending explicit privacy, device-security and conflict-policy approvals. This package is teaching content, not a hidden learner-data outbox.”

[If no Published lesson exists, use this fallback.]

> “The current lesson has not completed publication, so a complete approved offline package is not available from it. I will show the export controls and the preparation requirement, rather than claim this draft has passed offline delivery.”

### Device support

[As an authorised administrator/reporting account, open **Devices**.]

> “Device support gives authorised staff visibility into real registered devices, their active or revoked state, last activity and credential expiry.
>
> “Catalogue checkpoints represent acknowledged catalogue positions. They are operational synchronisation information, not learner progress.”

[If a disposable registered device exists, show its details and explain **Revoke access** without revoking a real device. If none exist, show the empty state.]

> “This browser does not invent a device registration or simulate a sync record. If no device is registered, the page says so. Device credentials are not exposed in this support view.”

## 15. Platform administrator — audit and close

[Return to PlatformAdmin. Open **Audit trail**.]

> “I will close with accountability. The audit trail records bounded operational events using identifiers and support references, without displaying passwords, tokens or full personal-data snapshots.”

[Filter using an event type actually present and a suitable date range; click **Apply filters**.]

> “These filters help an authorised operator investigate an event and relate it to an actor, organisation and support reference. This is an operational audit view, not an unrestricted copy of every database change.”

[Open **Your account**. Show profile, organisation memberships and security controls without changing the presenter's password.]

> “Each person can inspect their account and organisation memberships. Password changes and signing out everywhere are real security actions. I will not change presentation credentials on screen.”

[Return to the workspace or a clean published resource view.]

> “We have followed the platform from administration and curriculum through approved content, teacher planning, classroom delivery and authorised learner assessment.
>
> “The roles serve different responsibilities. Administrators govern access and operations. Content managers prepare resources. Teachers make the teaching decisions. Reviewers control publication. Learners use approved content and submit authorised online assessments. Guardians see explicitly linked learner context, while project managers and analysts work within their permitted organisational scope.
>
> “AI provides proposals that people review. Offline delivery supports teaching content, while identifiable offline learner progress remains subject to explicit approval.
>
> “Thank you. I am happy to return to any part of the journey and discuss the classroom or operational requirements in more detail.”

[At the end, sign out appropriately. Use **Sign out everywhere** only if you intend to revoke every session for that account.]

## Short 20-minute route

Use prepared records; do not attempt to create every relationship live.

| Time | Screen and script segment |
| --- | --- |
| 0–2 min | Opening, PlatformAdmin workspace and organisation boundary |
| 2–4 min | People/memberships, curriculum connection, brief OrganisationAdmin contrast |
| 4–7 min | ContentManager activity preview and separate reviewer publication |
| 7–11 min | Teacher plan, selected source, AI proposal review/apply or honest fallback |
| 11–13 min | HeadTeacher review and publication; use pre-reviewed material |
| 13–16 min | Teacher Prepare → Teach → activity → reveal → recap → Finish |
| 16–18 min | Learner published resource and one online assessment; briefly show guardian/project/analyst views if sessions are prepared |
| 18–20 min | Offline/privacy boundary, operational audit and closing |

If every role needs a substantial live turn, use the full route. Do not describe a brief account contrast as a complete test of that role.

## Honest fallback lines — keep beside the presenter

**Missing account or permission**

> “This role's account or membership is not prepared in the current environment. I will show the intended workflow without claiming that this permission boundary has been demonstrated under that role.”

**No published resource or assessment**

> “This item has not completed its publication or assignment workflow. The learner-facing empty state is correct; I will continue with material that has completed those steps.”

**AI delay or failure**

> “The live request has not completed successfully within this demonstration window. I will continue with the saved lesson. Any earlier proposal I show will be identified as an earlier result.”

**Unexpected error**

> “That action did not complete as expected. I will not describe it as successful. I will use the prepared fallback and record the issue for follow-up.”

**PDF blocked offline**

> “The browser has blocked the embedded PDF viewer. The separate approved PDF or printed guide is the fallback; the activity and document-viewer behaviour are separate checks.”

## Supporting reference

Use `docs/platform-administrator-walkthrough.md` for detailed administrative setup and troubleshooting. The earlier `docs/demo-readiness-2026-10-20.md` describes a separate isolated demo rehearsal; do not treat its accounts, screenshots or published lesson as current actual-database evidence.

This script was prepared from the implemented routes, controls and current read-only record inventory. Writing it did not create accounts, publish content, approve AI processing or test every role's authenticated browser journey.
