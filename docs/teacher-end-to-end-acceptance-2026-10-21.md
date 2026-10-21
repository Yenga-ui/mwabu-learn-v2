# Actual teacher end-to-end acceptance - 2026-10-21

**Update:** the initial failure below was reproduced, fixed and retested later the same evening. See “Fix and actual-app retest” for the current implementation and its limits; the original observations remain as regression evidence.

Executed 21 October 2026, completed about 18:12 Africa/Lusaka. Overall result: **FAIL - saved-position resume is missing**. This is an actual connected browser journey, not a collection of unrelated page screenshots or a synthetic fixture run.

## Environment and identity

- Application: https://localhost:7042, actual existing local `mwabu_learn` database and existing content storage; updated staged Sprint 4 build started after the presenter finished.
- Account verified through Your account: **Teacher Teacher**, role **Teacher**, organisation **Mwabu Education Zambia**. No PlatformAdmin session, role changes, password resets, API impersonation or direct database preparation was used for this journey.
- Actions performed through the signed-in browser UI. No production deployment, production database operation or publication occurred.
- New test record is a clearly labelled private draft: **Teacher E2E - Equivalent fractions - 21 October 2026** (the actual title uses em dashes).
- Draft: https://localhost:7042/app/lessons/b5625085-a95f-48f6-bcb9-01a6a1bd9258
- Final saved revision: **5**. Original source resource: `90e4e4be-3aaa-4738-8f40-70f0f422a2d7`.

## Connected execution and observed outcomes

| Step | Actual action and observation | Result |
|---|---|---|
| Verify teacher | Opened Your account; organisation membership explicitly showed Teacher. Teacher navigation excluded platform administration tools. | PASS |
| Find | Opened Find a lesson through navigation. Selected 2023, Grade 4, Mathematics, Fractions; searched Equivalent fractions. Three resources were returned, including the source plan and associated course. | PASS |
| Read original | Opened Equivalent fractions. Original HTML preview included source diagrams, competence 4.3.1.1, teaching stages and copyright. Missing term was not invented. | PASS |
| Adapt | Clicked Use this lesson. App created a separate teacher-owned draft, initially revision 2, retaining original-source attribution and linked resources. | PASS |
| Edit/save | Changed title, duration to 45 minutes, and Introduction learner activities to “Fold a paper strip into two equal parts. Explain why 12 out of 24 stitches is one half.” Set seven official stage times to 5/20/8/0/5/4/3. Saved. | PASS |
| Reopen | Returned through All lesson plans. Card identified “Your lesson”, 45 minutes, two attached resources and revision 4. A full browser reload of the reopened draft confirmed title, 45 minutes, learner edit and stage timing values persisted on the server. | PASS, with cache issue below |
| Export | Clicked Export saved PDF on saved revision 4, captured the actual browser download, retained its exact bytes, rendered and visually inspected all three pages. Name Teacher Teacher, Grade 4, 45 minutes, source competence, exact learner edit and full continuation content were present. Seven official stage rows retained; no extra duration column introduced. | PASS for saved-value/visual export |
| Prepare | Opened Prepare Lesson Journey at revision 4. The six nonempty teaching steps totalled 45/45 minutes; the empty zero-minute Homework stage remained in the official form but did not become a classroom step. Added controlled introductory hint and answer and saved preparation as revision 5. | PASS |
| Teach/reveal | Opened Teach Lesson pinned to revision 5. Intro showed the saved learner text. Show hint and Reveal answer displayed the entered hint and “12 stitches is one half of 24.” Next step cleared the reveal. Teacher instructions were not displayed in the classroom text. | PASS |
| Open real content | At Lesson development, step 2 of 6, selected the linked approved fraction course, clicked Start interactive lesson, and observed the actual 21-slide Storyline package. Clicked its next button; slide changed from 1/21 to 2/21 (“In this lesson you will learn…”). | PASS for launch and advancement; not an exhaustive 21-slide assessment/audio test |
| Leave/resume | Exited to preparation, returned to Workspace, reloaded, and used the named lesson under Continue where you left off. Reopened preparation and Teach Lesson. Same saved revision 5 reopened at Introduction, step 1/6; previously selected resource and course slide were lost. No direct saved-position resume action was offered. | **FAIL** |
| Reveal reset | Returned classroom view offered Show hint and Reveal answer; previous revealed text was not automatically restored. | PASS |
| Finish | Used overview to jump to Conclusion (step 4), then Finish lesson and returned to lesson list. Conclusion was reachable, but contained generic “Listen, discuss and explore together.” rather than a prepared learner-facing recap. | Navigation PASS; recap-content gap |
| Preserve original | Reopened original resource through the draft's source link. Original title, source teaching text, diagrams and attribution remained; teacher test title/activity were confined to the new copy. | PASS for observed original UI content |

## Evidence

Directory: `artifacts/sprint4/teacher-e2e-2026-10-21/`.

- `teacher-equivalent-fractions.pdf`: exact browser download from revision 4, 154253 bytes. SHA-256 `6757da545031f7c6c968b59a54603a2ff9f39a6c0d18b0fd9f9b445080f1646f`.
- `pdf-page-1.png`, `pdf-page-2.png`, `pdf-page-3.png`: all exported pages rendered at 1250px and visually inspected. No clipped/overlapping continuation text observed. Unknown date/time and uncompleted lesson-goal/evaluation fields remained blank. The inherited learner-count default of 30 is test data, not a verified real class enrolment.
- `pdf-text.txt`: diagnostic extraction only. The PDF's text extraction did not reliably reconstruct every visually correct value; accessible/selectable text conformance has not been established by this acceptance.
- `saved-adaptation.png`: persisted edited progression at revision 4.
- `classroom-course-slide-2.png`: same teacher's adapted plan, revision 5, step 2 with running real course at slide 2/21.
- `resume-failure-step-1.png`: same draft/revision after attempted resume, reset to step 1 with hidden answer.
- `completed-teacher-draft.png`: final saved revision 5, left open in the teacher browser.

## Reproducible issues and limits

1. **Release blocker for the requested journey: true resume fails.** Store an authorised, user/organisation-scoped pinned lesson revision, step and supported resource bookmark subject to the approved device/privacy policy; reauthorise before rendering. Never persist revealed answers or identifiable learner progress. Unsupported SCORM slide bookmarking must be labelled honestly and reopen the resource safely.
2. **Stale editor cache after save and navigation.** First SPA reopening after revision 4 was listed briefly showed the old revision 2 values. A full reload showed the correct revision 4 server values. Do not treat reload as a product fix; saved-query invalidation/editor reinitialisation needs regression coverage.
3. **Learner-facing recap is not automatically prepared.** Source conclusion was preserved as private teacher instructions, while classroom Conclusion used generic fallback text. A teacher can prepare explicit learner text, but this imported draft is not a complete recap journey out of the box.
4. **Stale resource metadata.** The course card still says download only / does not play in Mwabu Learn even though approved playback works. Review and version metadata through normal content governance.
5. Storyline included a browser-blocked nested subframe while main lesson rendered and advanced. Isolation was not weakened. Full narration, all questions and physical classroom audio/projector acceptance remain untested here.
6. This test did not exercise AI generation, offline playback, different organisations, mobile hardware or a complete accessibility audit; earlier isolated automated regression results do not substitute for those actual-account acceptance checks.

No implementation or security-control change was made for this acceptance run. A complete green teacher end-to-end result **cannot yet be claimed** because resume failed.

## Fix and actual-app retest - evening 21 October 2026

The initial failed acceptance above is retained as historical evidence. The following fixes were implemented and retested with the same existing Teacher account in the actual application:

- A dedicated Continue teaching card reauthorises the saved classroom revision before showing its title. Browser-session-only navigation data retains lesson/revision/step/resource IDs, expires after 12 hours, is scoped by teacher and organisation, and clears on Finish, sign-out or session expiry. It stores no learner identities, answers, lesson text, private notes, course tokens or model state and performs no progress synchronisation.
- Continue teaching returned to saved revision 5, step 2, with the approved course selected after leaving and reloading. A second run repeated this on revision 6. Revealed answers remained hidden. The course explicitly reopens from the beginning: exact SCORM slide resume is unsupported and is not claimed.
- Saving now updates the detail query cache, list and teacher-home queries. A new learner edit (“Compare one half with two quarters.”) was saved as revision 6, then immediately reopened through the list without a reload and observed intact.
- The exact revision-6 browser PDF was downloaded, rendered and all three pages visually inspected. It includes the extra learner sentence, teacher name, 45-minute duration and original template stages. Evidence: `artifacts/sprint4/teacher-fixes-2026-10-21/teacher-fractions-revision-6.pdf`, SHA-256 `4cc6c35dd5f874d841cce04c291ffb0d17f19f360c75c6c28f614251fb053fd5`.
- Latest real course launch/advance was verified at revision 6, step 2, course slide 2/21, then left/reloaded/resumed at step 2 with the resource restored. Anonymous course state is intentionally not retained.
- Empty learner-facing Conclusion/Recap now shows the reflection prompt “What did you learn today? Explain one idea in your own words and share an example.” This is a neutral fallback, not copied private teacher instructions or a claim of a complete curriculum-specific authored recap. Explicit teacher-written learner content takes precedence.
- Account remains an explicit settings destination. A return URL for Account now resolves to the functional workspace after sign-in; explicit logout clears its previous route. Valid lesson/resource return links remain supported. Password recovery/change functionality and authentication controls remain in place.
- A full regression exposed the API paging model binding selecting the `page` prefix and ignoring flat `text/pageSize` fields. Directory, education and lesson PageRequest parameters now explicitly bind flat query fields. The previously failing case now passes with HTTP assertions for exact text, page size 1, second page and invalid page 0 rejection.

Actual-app evidence: `artifacts/sprint4/teacher-fixes-2026-10-21/` includes `resumed-revision-6-step-2.png`, `actual-course-revision-6-slide-2.png` and `teacher-functional-home.png`. The Teacher browser is left on the functional workspace, not the Account form.

Remaining limits: historical course title/description still need content-owner review; a vendor subframe is blocked by browser isolation while the main course works; exhaustive course/audio/hardware acceptance, accessible PDF text-layer conformance, cross-device resume and offline SCORM are not claimed. No production changes or migrations were applied for these fixes.
