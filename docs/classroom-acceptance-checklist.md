# Classroom acceptance checklist

Automated acceptance and physical hardware acceptance are different. Check off a hardware item only after recording device model, Android/WebView/browser version, projector, adapter/protocol, operator, date and observed result. No production curriculum records are fabricated for tests.

## Automated release verification

- [ ] Backend contracts and API: old revision compatibility, step validation, pinned versions, stale-save conflicts, approval/revocation, organisation access and public response privacy.
- [ ] Frontend: legacy planning guidance is not projected; hints/answers/explanations are absent before reveal; navigation clears them; no whole-class scores or learner persistence.
- [ ] Browser: existing teacher/learner/assessment and content workflows continue to pass.
- [ ] Representative Grade 4 Mathematics fractions: managed curriculum, private preparation, approved attachment, saved revision, teaching steps, fraction manipulation, reveal/reset, guided practice, recap and exit.
- [ ] Simulated 390×844 preparation and 844×390 teaching; no horizontal overflow; reload preserves revision; fullscreen gracefully falls back.
- [ ] Published offline package contains exact dependencies and no private step/session fields; real file playback makes zero HTTP requests; interrupted-download regression remains green.
- [ ] Release build, production web build, lint/type checking, complete backend/frontend/browser suites, current vulnerability audits, no EF drift, historical migrations unchanged and hosted container smoke pass.

Execution evidence and exact results belong in sprint-3-implementation-report.md. The checkboxes here remain a reusable acceptance form, not a claim that hardware was tested.

## Physical school hardware — required before classroom release

- [ ] Android phone: portrait preparation, keyboard/input focus, touch scrolling, reliable back/reload behavior, large touch targets and safe logout/account switching.
- [ ] Supported projector: wired and/or approved wireless setup, landscape readable at rear classroom distance, correct aspect ratio, contrast in daylight and no overscan/clipped controls.
- [ ] Mirroring privacy: notes, school/user details and administrative content never appear in Teach Lesson. Stop mirroring before leaving for preparation; check system notifications separately using device do-not-disturb settings.
- [ ] Fullscreen supported/denied/exit/back behavior, orientation changes, sleep/wake, lock/unlock and casting reconnection; no promise of forced orientation.
- [ ] Audio/video: routing to projector/speakers, play/pause/restart, captions/alternatives, codecs and offline playback.
- [ ] Manipulatives: fractions, number line, place values, sequencing and questions can be operated accurately by touch; keyboard/screen-reader equivalents and reduced-motion behavior reviewed.
- [ ] Long lesson: at least 60 minutes with media and repeated activities; record memory pressure, battery/heat, dropped frames, audio stability and interruption recovery.
- [ ] Poor/no connectivity: download interrupted/retried, quota full, damaged bytes rejected, obsolete version removed, exported package playback remains functional without learner accounts.
- [ ] Independent external display: explicitly unsupported until a hardware-specific native/browser feasibility test passes. Do not expose private controls based on an assumed second display.
- [ ] Educator review: curriculum accuracy, language, reading level, pacing, large-class interaction and inclusive low-resource alternatives.
- [ ] Operator approval: portable export handling, retention, device security and provider educational quality. Identifiable offline learner synchronization remains disabled.
