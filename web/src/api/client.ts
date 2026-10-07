export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public fields: Record<string, string[]> = {},
    public correlationId?: string,
  ) {
    super(message);
  }
}
const messages: Record<number, string> = {
  400: "Check the information below and try again.",
  401: "Your session has ended. Sign in again.",
  403: "You do not have access to this action in this organisation.",
  404: "This item is no longer available.",
  409: "This item changed or already exists. Refresh and try again.",
  413: "This file exceeds the upload limit.",
  429: "Please wait a moment before trying again.",
  503: "This service is currently unavailable. Please try again later.",
};
export const sessionEvents = new EventTarget();
let renewalBlocked = false;
const channel =
  typeof BroadcastChannel !== "undefined"
    ? new BroadcastChannel("mwabu-session")
    : undefined;
channel?.addEventListener("message", () => {
  renewalBlocked = false;
  sessionEvents.dispatchEvent(new Event("changed"));
});
export function sessionChanged() {
  renewalBlocked = false;
  channel?.postMessage("changed");
  sessionEvents.dispatchEvent(new Event("changed"));
}
async function raw(path: string, init: RequestInit = {}) {
  if (!path.startsWith("/api/") || path.startsWith("//"))
    throw new Error("Only same-origin API routes are permitted.");
  return fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    headers: { Accept: "application/json", ...init.headers },
  });
}
export async function csrf(): Promise<string> {
  const response = await raw("/api/browser/session/csrf");
  if (!response.ok)
    throw new ApiError(
      response.status,
      "Unable to secure your request. Reload this page.",
    );
  return ((await response.json()) as { requestToken: string }).requestToken;
}
let refreshing: Promise<boolean> | undefined;
export function ensureSession(minValiditySeconds = 0): Promise<boolean> {
  if (renewalBlocked) return Promise.resolve(false);
  if (refreshing) return refreshing;
  refreshing = (async () => {
    // Web Locks serialize rotations across tabs. Inside the lock, a newer shared
    // HttpOnly cookie may already be valid; never rotate it a second time unnecessarily.
    if (!navigator.locks) return false;
    return navigator.locks.request("mwabu-refresh", async () => {
      const status = await raw("/api/browser/session");
      if (status.ok) {
        if (minValiditySeconds === 0) return true;
        const state = (await status.json()) as {
          expiresAt: string;
          serverTime: string;
        };
        if (
          Date.parse(state.expiresAt) - Date.parse(state.serverTime) >
          minValiditySeconds * 1000
        )
          return true;
      }
      if (!status.ok && status.status !== 401)
        throw new ApiError(
          status.status,
          messages[status.status] ?? "Unable to check your session.",
        );
      const token = await csrf();
      let response: Response;
      try {
        response = await raw("/api/browser/session/refresh", {
          method: "POST",
          headers: { "X-Mwabu-CSRF": token },
        });
      } catch {
        renewalBlocked = true;
        sessionEvents.dispatchEvent(new Event("expired"));
        throw new ApiError(
          401,
          "Session renewal could not be confirmed. Sign in again.",
        );
      }
      if (response.ok) return true;
      if (response.status === 401) return false;
      renewalBlocked = true;
      sessionEvents.dispatchEvent(new Event("expired"));
      // In particular, do not retry a 409 rotation conflict or a lost refresh response.
      throw new ApiError(
        response.status,
        messages[response.status] ??
          "Unable to renew your session. Sign in again.",
      );
    });
  })().finally(() => {
    refreshing = undefined;
  });
  return refreshing;
}
async function result<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body: {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
      correlationId?: string;
    } = await response.json().catch(() => ({}));
    throw new ApiError(
      response.status,
      response.status < 500
        ? (body.detail ??
            body.title ??
            messages[response.status] ??
            "Request failed.")
        : (messages[response.status] ??
            "Something went wrong. Please try again."),
      body.errors,
      body.correlationId ??
        response.headers.get("X-Correlation-Id") ??
        undefined,
    );
  }
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}
export async function api<T>(
  path: string,
  options: {
    method?: string;
    body?: unknown;
    signal?: AbortSignal;
    organisationId?: string;
    retryAuth?: boolean;
  } = {},
): Promise<T> {
  const browserMutation =
    options.method === "POST" && path.startsWith("/api/browser/session/");
  if (browserMutation && navigator.locks) {
    if (path.endsWith("/logout-all") && !(await ensureSession()))
      throw new ApiError(401, messages[401]);
    return navigator.locks.request("mwabu-refresh", () =>
      sendApi<T>(path, { ...options, retryAuth: false }),
    );
  }
  return sendApi<T>(path, options);
}
async function sendApi<T>(
  path: string,
  options: {
    method?: string;
    body?: unknown;
    signal?: AbortSignal;
    organisationId?: string;
    retryAuth?: boolean;
  },
): Promise<T> {
  const method = options.method ?? "GET";
  const headers: Record<string, string> = {};
  if (options.organisationId)
    headers["X-Organisation-Id"] = options.organisationId;
  if (method !== "GET") headers["X-Mwabu-CSRF"] = await csrf();
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  const init: RequestInit = {
    method,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  };
  let response = await raw(path, init);
  if (response.status === 401 && options.retryAuth !== false) {
    if (await ensureSession()) {
      options.signal?.throwIfAborted();
      if (method !== "GET") headers["X-Mwabu-CSRF"] = await csrf();
      response = await raw(path, init);
    } else sessionEvents.dispatchEvent(new Event("expired"));
  }
  return result<T>(response);
}
export function queryString(
  values: Record<string, string | number | boolean | undefined | null>,
) {
  const query = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "")
      query.set(key, String(value));
  });
  return query.toString();
}
export async function upload(
  path: string,
  file: File,
  assetType: string,
  onProgress: (percent: number) => void,
  signal: AbortSignal,
) {
  signal.throwIfAborted();
  await ensureSession();
  const token = await csrf();
  signal.throwIfAborted();
  return new Promise<void>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", path);
    xhr.setRequestHeader("X-Mwabu-CSRF", token);
    xhr.withCredentials = true;
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable)
        onProgress(Math.round((e.loaded * 100) / e.total));
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) resolve();
      else {
        let detail: string | undefined;
        try {
          detail = (JSON.parse(xhr.responseText) as { detail?: string }).detail;
        } catch {
          /* Proxy response may not be JSON. */
        }
        reject(
          new ApiError(
            xhr.status,
            detail ?? messages[xhr.status] ?? "Upload failed. Try again.",
          ),
        );
      }
    };
    xhr.onerror = () =>
      reject(
        new Error(
          "Connection lost during upload. Check the resource before retrying.",
        ),
      );
    xhr.onabort = () =>
      reject(new DOMException("Upload cancelled.", "AbortError"));
    signal.addEventListener("abort", () => xhr.abort(), { once: true });
    const form = new FormData();
    form.append("File", file);
    form.append("AssetType", assetType);
    xhr.send(form);
  });
}
