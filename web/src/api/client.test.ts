import { beforeEach, describe, expect, it, vi } from "vitest";
function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
beforeEach(() => {
  vi.resetModules();
  vi.stubGlobal(
    "BroadcastChannel",
    class extends EventTarget {
      postMessage() {}
    },
  );
  Object.defineProperty(navigator, "locks", {
    configurable: true,
    value: {
      request: vi.fn(async (_name: string, work: () => Promise<boolean>) =>
        work(),
      ),
    },
  });
});
describe("same-origin session transport", () => {
  it("shares one refresh among simultaneous expired requests", async () => {
    let valid = false;
    let rotations = 0;
    const fetch = vi.fn(async (path: string, options?: RequestInit) => {
      expect(options?.credentials).toBe("same-origin");
      if (path.endsWith("/csrf")) return json({ requestToken: "csrf" });
      if (path.endsWith("/refresh")) {
        rotations++;
        await new Promise((resolve) => setTimeout(resolve, 10));
        valid = true;
        return json({});
      }
      if (path.endsWith("/session")) return json({}, valid ? 200 : 401);
      return json({ title: "Resource" }, valid ? 200 : 401);
    });
    vi.stubGlobal("fetch", fetch);
    const { api } = await import("./client");
    const values = await Promise.all([
      api("/api/resource"),
      api("/api/resource"),
      api("/api/resource"),
    ]);
    expect(rotations).toBe(1);
    expect(values).toHaveLength(3);
  });
  it("rechecks shared cookies inside the cross-tab lock before rotating", async () => {
    const fetch = vi.fn(async (path: string) => {
      expect(path).toBe("/api/browser/session");
      return json({ authenticated: true });
    });
    vi.stubGlobal("fetch", fetch);
    const { ensureSession } = await import("./client");
    expect(await ensureSession()).toBe(true);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(fetch.mock.calls[0][0]).toBe("/api/browser/session");
  });
  it("does not retry a refresh conflict or lost rotation response", async () => {
    const fetch = vi.fn(async (path: string) =>
      path.endsWith("/csrf")
        ? json({ requestToken: "csrf" })
        : json({}, path.endsWith("/refresh") ? 409 : 401),
    );
    vi.stubGlobal("fetch", fetch);
    const { ensureSession } = await import("./client");
    await expect(ensureSession()).rejects.toMatchObject({ status: 409 });
    expect(await ensureSession()).toBe(false);
    expect(
      fetch.mock.calls.filter(([path]) => path.endsWith("/refresh")),
    ).toHaveLength(1);
  });
  it("clears authenticated state on an unrecoverable session and never uses web storage", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (path: string) =>
        path.endsWith("/csrf") ? json({ requestToken: "csrf" }) : json({}, 401),
      ),
    );
    const { api, sessionEvents } = await import("./client");
    const expired = vi.fn();
    sessionEvents.addEventListener("expired", expired);
    await expect(api("/api/private")).rejects.toMatchObject({ status: 401 });
    expect(expired).toHaveBeenCalledOnce();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
  it("requires an explicitly coordinated refresh in unsupported browsers", async () => {
    Object.defineProperty(navigator, "locks", {
      configurable: true,
      value: undefined,
    });
    const fetch = vi.fn();
    vi.stubGlobal("fetch", fetch);
    const { ensureSession } = await import("./client");
    expect(await ensureSession()).toBe(false);
    expect(fetch).not.toHaveBeenCalled();
  });
  it("sends antiforgery with cookie-authenticated writes and preserves useful ProblemDetails", async () => {
    const fetch = vi.fn(async (path: string, options: RequestInit) => {
      if (path.endsWith("/csrf"))
        return json({ requestToken: "protected-request-token" });
      expect(options.credentials).toBe("same-origin");
      expect(options.headers).toMatchObject({
        "X-Mwabu-CSRF": "protected-request-token",
      });
      return json(
        { detail: "Code already exists.", correlationId: "support-123" },
        409,
      );
    });
    vi.stubGlobal("fetch", fetch);
    const { api } = await import("./client");
    await expect(
      api("/api/organisations", {
        method: "POST",
        body: { code: "DUPLICATE" },
      }),
    ).rejects.toMatchObject({
      status: 409,
      message: "Code already exists.",
      correlationId: "support-123",
    });
  });
  it.each([403, 413, 429, 503, 500])(
    "handles HTTP %s without exposing server internals",
    async (status) => {
      vi.stubGlobal(
        "fetch",
        vi.fn(async () => json({ detail: "Sensitive provider error" }, status)),
      );
      const { api } = await import("./client");
      try {
        await api("/api/resource");
      } catch (error) {
        expect(error).toMatchObject({ status });
        if (status >= 500)
          expect((error as Error).message).not.toContain("Sensitive");
      }
    },
  );
});
