# Teacher navigation and guided AI planning — 22 October 2026

## Implemented journey

The signed-in legacy teacher experience was inspected at Grade 4 → Mathematics → Fractions → Common fractions. Its useful pattern is progressive curriculum discovery, a readable teacher plan and adjacent teaching content. The v2 grade/subject/topic discovery remains the entry point; copying Moodle administration menus would add unnecessary teacher tasks.

From the teacher workspace, choose **Create with AI**, select an available grade, subject and topic, then save the draft. The AI screen now asks classroom questions instead of requiring a technical operation and a written prompt:

1. Your class: duration, learner count, language and classroom materials. Materials have selectable choices and an optional description.
2. Learning and support: optional learning goals, prior knowledge/classroom context and support needs, with examples and a reminder to exclude personal information.
3. Review your answers: check the saved context before requesting the whole lesson.

**Create my lesson plan** uses the existing durable generation queue. Answers are stored in the lesson setup, not temporary browser storage, and the saved revision is used for generation. The teacher can return later, cancel a request, review a result and explicitly apply it to a draft. Publication remains a separate authorised review process. Advanced section operations and additional instructions remain under **More options**; section refinement opens these controls automatically.

Full-generation results now have a readable lesson preview. The before/after comparison remains available under **Compare with my current plan**. Existing section refinements retain the comparison view. The lesson list also has a direct **Create with AI** entry for authorised teachers; manual creation starts with topic choices.

## Verification

- TypeScript, strict ESLint and production build passed.
- Frontend suite: 92 tests across 23 files passed.
- Focused browser suite: five passed, covering guided generation/review/apply/reload, background recovery, cancellation, provider failure preservation and topic-first creation.
- Frontend dependency audit: zero advisories at all severities.
- Full browser regression: 57 passed, including real Product Team SCORM playback, offline activities, PDF/teaching journeys, role boundaries and session recovery.
- Final guided-question component retest passed after the language/material choices were added.
- Final production-asset browser retest: three passed (guided question journey, whole-plan preview/apply, section refinement).
- No backend contracts, permissions, database model, migrations or publication controls were changed.

## Actual teacher-session rehearsal and limitations

The existing local Teacher account opened the saved private QA draft **Completion QA — Grade 2 Recreation — 21 October 2026**, ID `13defb13-da3c-49b1-94c0-dd086877c8d5`. The new questions were used to select paper/pencils, enter a fair-play learning goal and class-level support requirements, review the answers and request a real whole-lesson generation. The answers survived the save at revision 3. Generation completed and returned objectives, starter, development, assessment, conclusion and other configured sections.

The real proposal was **not applied or published**: its timings narrative disagreed with the structured durations, its assessment questions lacked suggested answers, and its accessibility advice was unsuitable. This is a real model-quality limitation, not evidence of a teaching-ready plan. Teacher review is required; this UI change does not certify model output. The complete application/reload workflow passed with the controlled provider in the browser suite. Live-provider quality and latency remain release concerns.

Evidence: `artifacts/teacher-guided-planning-2026-10-22/` (local, untracked). The original full-page capture shows the earlier comparison view and real provider output; later viewport captures show the updated screen.

## Scope still requiring product work

- User testing with teachers to tune the number and wording of questions.
- Better model output quality and predictable generation latency, including pedagogical review of questions, suggested answers, concrete activity instructions and disability support.
- Approved resource artwork/thumbnails and complete historical curriculum mappings, which are separate content dependencies.
- This iteration improves the existing teacher journey; it is not a claim that every platform screen has been redesigned.

Local review: `https://localhost:7042/app`. Launch through `scripts/start-local-review.cmd`; existing data and sessions are preserved. No deployment or merge into main was performed.

## Visual navigation follow-up

Grade tiles now include a graduation-cap icon in discovery and the workspace. Subject/topic choices in discovery and creation use consistent subject icons (mathematics, science, languages, creative subjects and social studies), with a book fallback. AI planning has a sparkle action and classroom, learning and review icons. All icons are decorative and retain visible text labels; selection and keyboard focus are unchanged. Eight relevant frontend tests, TypeScript, strict lint and the production build passed. This adopts the live site's progressive navigation pattern; it is not a pixel-for-pixel reproduction or a claim that every screen has received a complete visual redesign.

The lesson editor now exposes **Create Plan with AI**, with a sparkle icon, beside **Lesson plan** and **View resources**. It is removed from **More lesson tools**. Two final browser journeys passed: guided AI generation/review/apply and the complete resource-to-plan/adapt/teach journey. TypeScript, strict lint and production build also passed. The existing Teacher session confirmed the button is visible while More lesson tools is collapsed; screenshot: `artifacts/teacher-guided-planning-2026-10-22/visible-create-plan-ai.png`.
