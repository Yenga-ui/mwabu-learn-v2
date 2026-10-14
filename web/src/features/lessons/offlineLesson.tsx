import { renderToStaticMarkup } from "react-dom/server";
import { LessonPrint } from "./LessonPrint";
import type { Detail } from "./types";

export function offlineLessonHtml(detail: Detail, organisation: string) {
  const content = renderToStaticMarkup(
    <LessonPrint
      document={detail.document}
      organisation={organisation}
      teacher={detail.authorName}
      revision={detail.viewedRevision}
      status={detail.lesson.status}
      dirty={false}
    />,
  );
  // Self-contained, read-only, escaped React markup. No session tokens, scripts, fonts or network resources.
  return (
    '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">' +
    "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>Mwabu Learn — offline lesson</title>" +
    "<style>body{font:16px system-ui;color:#252334;max-width:1000px;margin:2rem auto;padding:1rem}h1,h2{color:#250e62}p,td{white-space:pre-wrap;overflow-wrap:anywhere}table{border-collapse:collapse;width:100%}th,td{border:1px solid #e7e5ec;padding:.5rem;vertical-align:top}.table-wrap{overflow:auto}dt{font-weight:bold}dd{margin:0 0 .5rem}@media print{body{margin:0}}</style></head><body>" +
    "<p>Offline saved copy. Read-only; this file cannot receive revocations or later revisions. Verify the current approved version when reconnecting. Store it securely and remove it when no longer authorised.</p>" +
    content +
    "</body></html>"
  );
}
