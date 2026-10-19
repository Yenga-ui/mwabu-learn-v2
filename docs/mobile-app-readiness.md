# Mobile application readiness and Sprint 4 recommendation

This sprint implements the website reference journey; it does not create a native app or establish physical projector acceptance. Public ClassroomLesson/ClassroomStep JSON, immutable resource dependencies, schema-3 lesson snapshots, schema-2 packages and the self-contained activity/whole-class renderers are reusable. React-specific preparation and routing can be packaged or adapted separately from the .NET API. No executable model-generated content is supported.

## Recommendation and evidence

Evaluate a Capacitor Android shell first, using a bounded pilot on school hardware. Its runtime supports reusing web code with native plugins, which suits the existing React application. This is an architectural recommendation based on code reuse, not a measured claim of Android performance. [Capacitor overview](https://capacitorjs.com/docs) describes that runtime/plugin model. Its [environment requirements](https://capacitorjs.com/docs/getting-started/environment-setup) should be rechecked when the pilot starts, rather than pinning an untested Android toolchain here.

| Concern | Capacitor pilot | Dedicated Kotlin alternative / acceptance requirement |
| --- | --- | --- |
| Code reuse | Reuse React preparation, fixed renderer, public contracts and manifest verification | Reimplement UI; reuse JSON, API and package semantics, with cross-platform contract fixtures |
| Offline storage | Add app-private file downloads and a transactional manifest/index; keep resumable ranges, checksums, quota eviction and atomic activation | Native storage integrates directly but still needs equivalent integrity/recovery policies |
| Media | Test WebView codecs, audio routing, controls, PDFs and bounded memory on target Android devices | Native players may help if measured codec/memory failures justify them |
| Compatibility | Maintain Android System WebView and test low-memory supported school phones | Native UI may reduce WebView dependency but increases implementation and renderer maintenance |
| Casting | Begin with device-supported screen mirroring, with explicit teacher setup | Native integrations remain hardware/protocol specific; never infer universal casting |
| Independent display | Treat as optional plugin feasibility work on supported hardware | Android Presentation/connected-display APIs offer a native route, subject to actual display capabilities |
| Updates | Signed APK/store/managed distribution; version API/package compatibility explicitly | Same signing/rollback and operator release requirements; no unreviewed remote executable updates |
| Device management | Assess school MDM, patching, screen lock, account removal and package wiping | Same organisational policy; native APIs may offer deeper integration where justified |
| Performance | Measure cold start, long-lesson heap, video playback, thermal/battery behavior and disconnect recovery | Choose native only when a prototype demonstrates a material unmet need |
| Security | Strict local origin/CSP, no untrusted WebView navigation, app-private storage and reviewed secure credentials | Same identity/retention/revocation controls; native code does not automatically provide them |

Browser IndexedDB is a resumable media cache, not the durable encrypted mobile content library. Capacitor's [storage guide](https://capacitorjs.com/docs/guides/storage) explains WebView storage eviction and the different roles of preferences and larger-data storage. Evaluate a maintained transactional database plugin and app-private files with an approved encryption/key-management approach; do not store credentials or learner progress in generic preferences. Identifiable offline progress remains disabled until privacy, device security, retention and conflict policies are approved.

## Projector feasibility

Reliable screen mirroring is the supported product assumption. The browser cannot create a universally independent projector view or promise to initiate casting. Chrome's [Window Management API documentation](https://developer.chrome.com/docs/capabilities/web-apis/window-management) describes a capability with availability and permission constraints; it is not evidence of universal Android projector support. Android's [connected display guidance](https://developer.android.com/develop/adaptive-apps/guides/support-connected-displays) and [Presentation API](https://developer.android.com/reference/android/app/Presentation) are appropriate references for a future native/plugin feasibility spike. Test wired output, wireless protocols, disconnection and display changes against the actual supported device/projector combinations before committing to independent displays.

Sprint 4 should first approve the offline device/security policy, inventory school hardware and run a signed Capacitor pilot that imports a schema-2 classroom package. Verify authentication expiry, restart recovery, app-private package storage, checksum failure, storage exhaustion, video/audio, 60-minute lessons, screen mirroring and safe account removal. Build a native external-display plugin only if hardware support and private-control requirements are demonstrated. A dedicated native application remains an alternative if that pilot exposes measured constraints that cannot be solved safely with shared rendering.
