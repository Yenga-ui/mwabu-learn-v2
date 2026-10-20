# Product Team content intake — 20 October 2026

## Result

The first local pilot contains **six published catalogue resources**: five teacher
plans and one original legacy SCORM archive. Publication used the signed-in
administrator's existing Content Studio permissions and the normal Draft →
InReview → Published workflow. No approval status was fabricated.

This is **not a completed bulk import or a working SCORM integration**. The plans
are catalogue resources with readable teaching notes and original HTML downloads;
they have not been converted into native Lesson Planner records. The SCORM ZIP is
explicitly labelled **download only** and does not execute inside the application.

## Published local records

Open `https://localhost:7042/app/resources`, search for `local test`, and select:

| Resource | Source collection | Resource ID |
| --- | --- | --- |
| Order sets according to size — Grade 1 | 2023 | `79730796-0e92-4025-a605-a65b7ee0ef86` |
| Collect data — Grade 2 | 2023 | `2113a910-96b1-4260-a419-22de56679a06` |
| Shape fractions (1 of 4) — Grade 3 | 2013 | `7f8476e9-f653-4cdc-bc49-59851d021264` |
| Equivalent fractions — Grade 4 | 2023 | `a076fec9-a31b-45df-a2ae-77a03a209f83` |
| Writing equivalent fractions — Grade 4 | 2023 | `21a39baf-259f-4f3e-bf6f-be05b2e4d4a9` |
| Equivalent fractions — legacy SCORM package | Legacy learner content | `d7a570e8-121b-4d80-979f-6599a05e4439` |

All titles identify local testing. Original copyright **© Mwabu Group Ltd** is
retained; no Creative Commons or other open licence was inferred. Sharing links
and source files remain in ignored local staging rather than this repository.

Added teaching notes distinguish the original from local accessibility and
demonstration guidance. The data lesson recommends fictional aggregate examples
for demonstrations. No learner records or progress were imported.

## Source inventory and integrity

Ignored staging directory: `.local/content-import/`.

| Downloaded source | Unique plans staged |
| --- | ---: |
| 2023 Grade 4 mathematics, ten topic ZIPs | 105 |
| 2023 Grade 1 grouping | 17 |
| 2023 Grade 2 data | 3 |
| 2013 Grade 3 fractions | 13 |
| Total | 138 |

The local preparation script reads archives without executing their HTML or
JavaScript. It checks paths, duplicate members, symlinks, member counts,
compression/expansion limits and CRCs. Files are staged under SHA-256 directories,
not source-controlled archive filenames. The 138 lesson HTML files contain no
script/iframe/object/embed/form elements; this is a structural check, not a full
content or accessibility certification. Most plans still require editorial review.

`published-import-manifest.json` records all six application IDs, slugs,
publication states and source checksums. Per-source manifests retain each original
member path, hash, source grade/competence claims and review state. Downloads from
the actual application matched the original SHA-256 for **all six resources**.

## Review findings and blockers

- The supplied Grade 4 plan “Converting mixed fractions and improper fractions
  (1 of 3)” contains inconsistent exercise denominators. It was not published.
- “Fractions revision” gives `1 5/8` for `2 3/8 − 1 3/4`; the result is `5/8`.
  It was not published. Originals were preserved, not silently corrected.
- Some plans in the 2023 folder label their subject “Maths & Science (2025>)”.
  Both facts are preserved. Folder membership alone is not a verified curriculum
  mapping.
- The local 2023 framework currently contains Grade 4 Mathematics, with a Whole
  Number topic; no Fractions topic or matching competence/outcome exists. The
  other local curriculum is labelled 2026/Version1 and contains Grade 2. There is
  no configured 2013 hierarchy. Imported resources therefore remain unmapped.
  No invented topic or incorrect whole-number mapping was used.
- Grades 5–7 and other subjects have not been imported in this pilot. No 2013
  Grade 1, 2 or 4 plans were loaded over the requested 2023 precedence.
- The Product Team mapping spreadsheet is still needed for reliable legacy-to-
  current placement. AI suggestions must be checked against actual curriculum IDs
  and reviewed; a model's confidence is not evidence of a correct mapping.

## Legacy interactive package

The downloaded `1046046_Equivalent fractions.zip` is 4,593,108 bytes; SHA-256:

`b273005982533263c427b47e48483e97d62976f61717dc5d09a58dd970c19fb1`

Static inspection found SCORM 1.2, launch `index_lms.html`, Articulate Storyline
3.59.26702.0, source publication date 21 May 2022, 21 slides, 137 files and
8,935,650 expanded bytes. It includes 23 MP3s, 64 PNGs and 31 JavaScript files.
Vendor analytics are enabled in the original. The archive has been preserved
unchanged and published as a download-only source record, not approved for
execution or learner tracking. No online/offline SCORM playback was claimed.

Before classroom playback, implement and test an isolated SCORM delivery boundary,
review analytics and external requests, preserve source/provenance separately from
any reviewed derivative, verify accessibility and marking, and test complete online
and offline journeys. Do not enable generic uploaded HTML execution in the main
application origin or collect identifiable learner progress as a shortcut.

## Verification and recovery

- Actual browser: administrator session, resource creation, upload, review,
  publication, published resource pages and all six downloads verified.
- Read-only database query independently confirmed six Published records, each
  with one asset.
- Existing application records were preserved. A new local database backup was
  created before importing: `.local/content-import/backup-before-import-20261020-160816.dump`.
- No application source, permissions, migrations, authentication or security controls
  changed. Full application suites and CI were not rerun for these data-only imports.
- No production database, deployment or branch merge was performed.
- To withdraw a pilot resource, use Content Studio's normal Archive action. Do not
  rewrite published records or restore the whole database over later user work.

Screenshots under `.local/content-import/`: `published-equivalent-fractions.jpg`,
`published-writing-equivalent-fractions.jpg`, `published-scorm-source.jpg`, and
`published-batch.jpg`.
