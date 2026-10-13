# Mwabu React branding

The managed logo at `web/public/brand/mwabu-logo.png` is a byte-identical copy of the supplied `mwabu logo.png`. The favicon is a deterministic crop/resize of its orange icon, not an internet substitute. `Brand.tsx` supplies the shared login, sidebar and mobile-header lockup.

The shared tokens in `web/src/app/styles.css` define Mwabu purple `#250E62`, orange `#EA5B0C`, white/secondary surfaces, text, borders, interaction states and semantic statuses. Lesson Planner CSS uses the same tokens. Purple buttons have white text; orange remains an accent, with a darker orange focus token. Measured contrast ratios: primary button 15.94:1, main text 15.34:1, secondary text on secondary surface 5.39:1, control border 4.08:1, focus on white 6.50:1, and semantic status text above 6.5:1. This verifies the chosen colour pairs, not a claim of a complete accessibility audit.

Verification: frontend typecheck, zero-warning lint, all 37 unit/component tests and production build passed. Real anonymous login browser checks passed at 1440, 768 and 390 pixels, with loaded logos, usable fields, no horizontal overflow or JavaScript errors. The full authenticated browser suite was not run locally for this urgent visual update because its isolated PostgreSQL fixture is not running; the existing branch CI still runs that suite. Authentication, API calls, routes, session security, database schema and lesson planning logic are unchanged.

## Refresh the current local demo

The API is running at `https://localhost:7042` and serves `backend/src/MwabuLearn.Api/wwwroot`. The updated build has already been copied there and verified through HTTPS. Press **Ctrl+F5** in the browser; no API restart is required. If the favicon remains cached, close and reopen the tab.

For subsequent changes, run from `C:\Development\mwabu-learn-v2` in PowerShell:

```powershell
pnpm --dir web build
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
Copy-Item -Path 'web/dist/*' -Destination 'backend/src/MwabuLearn.Api/wwwroot' -Recurse -Force
```

Then hard-refresh the browser. This copies compiled public assets only, preserves old hashed assets for already-open tabs, and does not alter application data. Generated `web/dist` and local `wwwroot` are not committed. The original supplied logo and `Claude outputs/` remain untouched.
