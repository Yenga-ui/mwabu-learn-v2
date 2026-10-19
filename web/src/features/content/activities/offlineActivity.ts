import { mountActivity, type ActivityDefinition } from "./activityRuntime";
export async function offlineActivityHtml(
  definition: ActivityDefinition,
  version: string,
) {
  const payload = JSON.stringify({ definition, version })
    .replaceAll("<", "\\u003c")
    .replaceAll("\u2028", "\\u2028")
    .replaceAll("\u2029", "\\u2029");
  const script = `const payload=JSON.parse(document.getElementById('activity-data').textContent);document.getElementById('version').textContent='Saved published version: '+payload.version;(${mountActivity.toString()})(document.getElementById('activity'),payload.definition);`;
  const digest = new Uint8Array(
    await crypto.subtle.digest("SHA-256", new TextEncoder().encode(script)),
  );
  const hash = btoa(String.fromCharCode(...digest));
  return `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'sha256-${hash}'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'"><title>Mwabu offline activity</title><style>body{font:18px system-ui;color:#252334;max-width:850px;margin:1rem auto;padding:1rem}button,select{font:inherit;padding:.5rem;margin:.3rem}fieldset{padding:1rem;margin:1rem 0}label{display:block;padding:.5rem}svg{width:100%;height:auto}.fraction-parts{display:flex}.fraction-parts button{flex:1;min-width:0;border:2px solid}.fraction-parts button[aria-pressed=true]{background:#250e62;color:white}input{margin-right:.5rem}*:focus-visible{outline:3px solid #a74206;outline-offset:3px}</style></head><body><h1>Mwabu learning activity</h1><p>This offline copy cannot receive revocations or updates. Check the approved version when reconnecting. Practice feedback stays in this page and is cleared when it closes.</p><p id="version"></p><main id="activity"></main><script type="application/json" id="activity-data">${payload}</script><script>${script}</script></body></html>`;
}
