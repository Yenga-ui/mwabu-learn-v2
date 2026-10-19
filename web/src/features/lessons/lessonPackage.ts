import {
  mountActivity,
  type ActivityDefinition,
} from "../content/activities/activityRuntime";
import { mountWholeClass } from "../content/activities/wholeClassRuntime";
export type PackageAsset = {
  id: string;
  title: string;
  mimeType: string;
  size: number;
  checksum: string;
  downloadUrl: string;
};
export type LessonPackage = {
  teachingSteps?: {
    id: string;
    title: string;
    phase: string;
    minutes: number;
    learnerExplanation: string;
    hint: string;
    answer: string;
    resources: { assetId: string; checksum: string }[];
  }[];
  schemaVersion: number;
  lessonId: string;
  revision: number;
  title: string;
  language: string;
  sections: { title: string; body: string }[];
  assets: PackageAsset[];
  resources?: {
    title: string;
    summary?: string;
    description?: string;
    language: string;
  }[];
  version: string;
};
type Cached = { key: string; bytes: Uint8Array };
const maximumStorage = 50 * 1024 * 1024;
function database(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open("mwabu-approved-media", 1);
    request.onupgradeneeded = () =>
      request.result.createObjectStore("assets", { keyPath: "key" });
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}
async function cached(key: string): Promise<Uint8Array> {
  const db = await database();
  try {
    return await new Promise((resolve, reject) => {
      const request = db.transaction("assets").objectStore("assets").get(key);
      request.onsuccess = () =>
        resolve(
          (request.result as Cached | undefined)?.bytes ?? new Uint8Array(),
        );
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}
async function store(key: string, bytes: Uint8Array) {
  const db = await database();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction("assets", "readwrite"),
        table = tx.objectStore("assets"),
        query = table.getAll();
      query.onsuccess = () => {
        const rows = query.result as Cached[];
        if (
          rows
            .filter((x) => x.key !== key)
            .reduce((sum, x) => sum + x.bytes.byteLength, 0) +
            bytes.byteLength >
            maximumStorage ||
          (rows.length >= 64 && !rows.some((x) => x.key === key))
        ) {
          tx.abort();
          return;
        }
        table.put({ key, bytes });
      };
      tx.oncomplete = () => resolve();
      tx.onabort = () =>
        reject(
          new Error("Download storage is full. Clear saved media and retry."),
        );
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}
export async function packageStorage() {
  const db = await database();
  try {
    return await new Promise<number>((resolve, reject) => {
      const request = db.transaction("assets").objectStore("assets").getAll();
      request.onsuccess = () =>
        resolve(
          (request.result as Cached[]).reduce(
            (sum, x) => sum + x.bytes.byteLength,
            0,
          ),
        );
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}
export async function clearPackageStorage() {
  const db = await database();
  try {
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction("assets", "readwrite");
      tx.objectStore("assets").clear();
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } finally {
    db.close();
  }
}
export async function sha256(bytes: Uint8Array) {
  const digest = new Uint8Array(
    await crypto.subtle.digest("SHA-256", bytes as Uint8Array<ArrayBuffer>),
  );
  return Array.from(digest, (x) => x.toString(16).padStart(2, "0")).join("");
}
export async function downloadPackageAsset(
  asset: PackageAsset,
  signal: AbortSignal,
  onProgress: (bytes: number) => void,
): Promise<Uint8Array> {
  if (
    !/^\/api\/learning\/content\/[a-f0-9-]+\/assets\/[a-f0-9-]+\/download$/.test(
      asset.downloadUrl,
    ) ||
    !Number.isSafeInteger(asset.size) ||
    asset.size < 1 ||
    asset.size > 25 * 1024 * 1024 ||
    !/^[a-f0-9]{64}$/i.test(asset.checksum)
  )
    throw new Error("Invalid package manifest.");
  let bytes = await cached(asset.checksum);
  if (bytes.byteLength > asset.size) bytes = new Uint8Array();
  while (bytes.byteLength < asset.size) {
    signal.throwIfAborted();
    const start = bytes.byteLength,
      end = Math.min(start + 1024 * 1024, asset.size) - 1;
    const response = await fetch(asset.downloadUrl, {
      credentials: "same-origin",
      signal,
      headers: {
        Range: `bytes=${start}-${end}`,
        "If-Range": `"${asset.checksum}"`,
      },
      cache: "no-store",
    });
    if (
      !response.ok ||
      (response.status !== 206 &&
        !(response.status === 200 && start === 0 && end === asset.size - 1)) ||
      response.headers.get("ETag") !== `"${asset.checksum}"` ||
      (response.status === 206 &&
        response.headers.get("Content-Range") !==
          `bytes ${start}-${end}/${asset.size}`)
    )
      throw new Error(
        "Download changed or access expired. Recheck the approved package before retrying.",
      );
    // Reject oversized responses before allocating their complete body, even if a server ignores Range.
    const reader = response.body?.getReader();
    if (!reader) throw new Error("Download stream unavailable.");
    const chunks: Uint8Array[] = [];
    let length = 0;
    try {
      while (true) {
        const part = await reader.read();
        if (part.done) break;
        length += part.value.byteLength;
        if (length > end - start + 1)
          throw new Error("Download exceeded its expected range.");
        chunks.push(part.value);
      }
    } catch (error) {
      await reader.cancel();
      throw error;
    }
    if (length !== end - start + 1)
      throw new Error(
        "Interrupted download. Completed chunks are kept for retry.",
      );
    const next = new Uint8Array(start + length);
    next.set(bytes);
    let at = start;
    for (const chunk of chunks) {
      next.set(chunk, at);
      at += chunk.byteLength;
    }
    bytes = next;
    await store(asset.checksum, bytes);
    onProgress(bytes.byteLength);
  }
  if ((await sha256(bytes)) !== asset.checksum.toLowerCase()) {
    await store(asset.checksum, new Uint8Array());
    throw new Error(
      "Checksum mismatch. This asset was discarded; retry the download.",
    );
  }
  return bytes;
}
type Embedded = PackageAsset & { data: string };
async function mountPackage(
  payload: LessonPackage & { assets: Embedded[] },
  mount: typeof mountActivity,
  wholeClass: typeof mountWholeClass,
) {
  const main = document.querySelector("main")!;
  document.documentElement.lang = payload.language;
  const text = (tag: string, value: string, parent: HTMLElement = main) => {
    const node = document.createElement(tag);
    node.textContent = value;
    parent.append(node);
    return node;
  };
  text("h1", payload.title);
  text(
    "p",
    `Published lesson revision ${payload.revision}. Package version ${payload.version}.`,
  );
  text(
    "p",
    "Offline copy: check the approved version when reconnecting. Remove obsolete copies. No learner answers, identity or progress are saved or uploaded.",
  );
  const steps = payload.teachingSteps ?? [];
  const activityCleanup = new Map<string, (() => void) | undefined>();
  const verifiedActivities = new Map<string, ActivityDefinition>();
  const stage = text("section", "");
  for (const section of steps.length ? [] : payload.sections) {
    text("h2", section.title);
    text("p", section.body);
  }
  for (const resource of payload.resources ?? []) {
    const host = text("section", "");
    host.lang = resource.language;
    text("h2", resource.title, host);
    if (resource.summary) text("p", resource.summary, host);
    if (resource.description) text("p", resource.description, host);
  }
  for (const asset of payload.assets) {
    const host = text("section", "");
    host.dataset.packageAsset = asset.id;
    host.hidden = steps.length > 0;
    text("h2", asset.title, host);
    try {
      const raw = Uint8Array.from(atob(asset.data), (x) => x.charCodeAt(0));
      const hash = Array.from(
        new Uint8Array(await crypto.subtle.digest("SHA-256", raw)),
        (x) => x.toString(16).padStart(2, "0"),
      ).join("");
      if (hash !== asset.checksum.toLowerCase())
        throw new Error("Checksum mismatch");
      if (asset.mimeType === "application/vnd.mwabu.activity+json") {
        const definition = JSON.parse(
          new TextDecoder().decode(raw),
        ) as ActivityDefinition;
        verifiedActivities.set(asset.id, definition);
        const cleanup = (
          steps.length
            ? (node: HTMLElement, definition: ActivityDefinition) =>
                wholeClass(node, definition, mount)
            : mount
        )(host, definition);
        activityCleanup.set(asset.id, cleanup);
        continue;
      }
      const src = `data:${asset.mimeType};base64,${asset.data}`;
      if (asset.mimeType.startsWith("image/")) {
        const img = document.createElement("img");
        img.src = src;
        img.alt = asset.title;
        host.append(img);
      } else if (
        asset.mimeType.startsWith("audio/") ||
        asset.mimeType.startsWith("video/")
      ) {
        const media = document.createElement(
          asset.mimeType.startsWith("audio/") ? "audio" : "video",
        );
        media.controls = true;
        media.preload = "none";
        media.src = src;
        host.append(media);
      } else if (asset.mimeType === "application/pdf") {
        const frame = document.createElement("iframe");
        frame.src = src;
        frame.title = asset.title;
        frame.setAttribute("sandbox", "");
        host.append(frame);
      } else throw new Error("Unsupported media");
    } catch {
      text(
        "p",
        "This saved resource failed integrity or format checks. Download the approved package again.",
        host,
      );
    }
  }
  if (steps.length) {
    let index = 0;
    const controls = text("nav", "");
    controls.setAttribute("aria-label", "Offline teaching controls");
    const button = (label: string, action: () => void) => {
      const node = text("button", label, controls) as HTMLButtonElement;
      node.type = "button";
      node.onclick = action;
      return node;
    };
    const previous = button("Previous step", () => {
      index--;
      show();
    });
    const next = button("Next step", () => {
      index++;
      show();
    });
    const select = document.createElement("select");
    select.setAttribute("aria-label", "Teaching step");
    steps.forEach((step, i) => {
      const option = document.createElement("option");
      option.value = String(i);
      option.textContent = `${i + 1}. ${step.title}`;
      select.append(option);
    });
    select.onchange = () => {
      index = Number(select.value);
      show();
    };
    controls.append(select);
    let hint = false,
      answer = false;
    const revealHint = button("Show hint", () => {
      hint = !hint;
      renderText();
    });
    const revealAnswer = button("Reveal answer", () => {
      answer = !answer;
      renderText();
    });
    function renderText() {
      const step = steps[index];
      stage.replaceChildren();
      text("h2", step.title, stage);
      text(
        "p",
        `${step.phase} · ${step.minutes} minutes · Step ${index + 1} of ${steps.length}`,
        stage,
      );
      text("p", step.learnerExplanation, stage);
      if (hint) text("p", `Hint: ${step.hint}`, stage);
      if (answer) text("p", `Answer: ${step.answer}`, stage);
      revealHint.textContent = hint ? "Hide hint" : "Show hint";
      revealAnswer.textContent = answer ? "Hide answer" : "Reveal answer";
    }
    function show() {
      hint = false;
      answer = false;
      previous.disabled = index === 0;
      next.disabled = index === steps.length - 1;
      select.value = String(index);
      revealHint.hidden = !steps[index].hint;
      revealAnswer.hidden = !steps[index].answer;
      main
        .querySelectorAll<HTMLElement>("[data-package-asset]")
        .forEach((host) => {
          host
            .querySelectorAll<HTMLMediaElement>("audio,video")
            .forEach((media) => media.pause());
          const assetId = host.dataset.packageAsset!;
          const cleanup = activityCleanup.get(assetId);
          cleanup?.();
          activityCleanup.delete(assetId);
          host.hidden = !steps[index].resources.some(
            (r) =>
              r.assetId === host.dataset.packageAsset &&
              payload.assets.some(
                (a) =>
                  a.id === r.assetId &&
                  a.checksum.toLowerCase() === r.checksum.toLowerCase(),
              ),
          );
          const asset = payload.assets.find((a) => a.id === assetId);
          const definition = verifiedActivities.get(assetId);
          if (
            !host.hidden &&
            asset?.mimeType === "application/vnd.mwabu.activity+json" &&
            definition
          ) {
            activityCleanup.set(assetId, wholeClass(host, definition, mount));
          }
        });
      renderText();
    }
    show();
  }
}
export async function lessonPackageHtml(
  manifest: LessonPackage,
  files: Uint8Array[],
) {
  if (![1, 2].includes(manifest.schemaVersion))
    throw new Error("Unsupported lesson package version.");
  if (files.length !== manifest.assets.length)
    throw new Error("Package is incomplete.");
  const assets: Embedded[] = [];
  for (let i = 0; i < files.length; i++) {
    if ((await sha256(files[i])) !== manifest.assets[i].checksum.toLowerCase())
      throw new Error("Package integrity verification failed.");
    let data = "";
    for (let at = 0; at < files[i].length; at += 16384)
      data += String.fromCharCode(...files[i].subarray(at, at + 16384));
    const asset = manifest.assets[i];
    assets.push({
      id: asset.id,
      title: asset.title,
      mimeType: asset.mimeType,
      size: asset.size,
      checksum: asset.checksum,
      downloadUrl: "",
      data: btoa(data),
    });
  }
  // Explicit whitelist: never carry preparation fields or session metadata into an export.
  const payload = JSON.stringify({
    schemaVersion: manifest.schemaVersion,
    lessonId: manifest.lessonId,
    revision: manifest.revision,
    title: manifest.title,
    language: manifest.language,
    version: manifest.version,
    sections: manifest.sections.map((s) => ({ title: s.title, body: s.body })),
    assets,
    resources: manifest.resources?.map((r) => ({
      title: r.title,
      summary: r.summary,
      description: r.description,
      language: r.language,
    })),
    teachingSteps: manifest.teachingSteps?.map((s) => ({
      id: s.id,
      title: s.title,
      phase: s.phase,
      minutes: s.minutes,
      learnerExplanation: s.learnerExplanation,
      hint: s.hint,
      answer: s.answer,
      resources: s.resources.map((r) => ({
        assetId: r.assetId,
        checksum: r.checksum,
      })),
    })),
  })
    .replaceAll("<", "\\u003c")
    .replaceAll("\u2028", "\\u2028")
    .replaceAll("\u2029", "\\u2029");
  const script = `const payload=JSON.parse(document.getElementById('package-data').textContent);(${mountPackage.toString()})(payload,(${mountActivity.toString()}),(${mountWholeClass.toString()}));`;
  const digest = new Uint8Array(
    await crypto.subtle.digest("SHA-256", new TextEncoder().encode(script)),
  );
  const hash = btoa(String.fromCharCode(...digest));
  return `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'sha256-${hash}'; style-src 'unsafe-inline'; img-src data:; media-src data:; frame-src data:; base-uri 'none'; form-action 'none'"><title>Mwabu offline lesson package</title><style>body{font:18px system-ui;max-width:1000px;margin:1rem auto;padding:1rem}p{white-space:pre-wrap}button,input,select{font:inherit;padding:.5rem;margin:.3rem}label{display:block}img,video{max-width:100%}iframe{width:100%;height:80vh}.fraction-parts{display:flex}.fraction-parts button{flex:1;min-width:0;border:2px solid}.fraction-parts button[aria-pressed=true]{background:#250e62;color:white}</style></head><body><main></main><script id="package-data" type="application/json">${payload}</script><script>${script}</script></body></html>`;
}
