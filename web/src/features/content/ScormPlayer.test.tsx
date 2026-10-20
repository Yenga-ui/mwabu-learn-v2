import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { runInNewContext } from "node:vm";
import ScormPlayer from "./ScormPlayer";
import { api } from "../../api/client";
import type { Asset } from "../../api/types";

vi.mock("../../api/client", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../api/client")>()),
  api: vi.fn(),
}));
const resourceId = "11111111-1111-1111-1111-111111111111";
const assetId = "22222222-2222-2222-2222-222222222222";
const asset: Asset = {
  id: assetId,
  fileName: "fractions.zip",
  mimeType: "application/zip",
  fileSizeBytes: 10,
  assetType: "Package",
  checksum: "hash",
  isPrimary: true,
  viewUrl: `/api/learning/content/${resourceId}/assets/${assetId}/view`,
  downloadUrl: "/download",
};

describe("SCORM playback boundary", () => {
  it("starts only on request and never grants same-origin sandbox access", async () => {
    const url = "/api/scorm/runtime/" + "a".repeat(43) + "/index.html";
    vi.mocked(api).mockResolvedValue({
      launchUrl: url,
      title: "Equivalent fractions",
    });
    const { container } = render(<ScormPlayer asset={asset} />);
    expect(container.querySelector("iframe")).toBeNull();
    fireEvent.click(
      screen.getByRole("button", { name: "Start interactive lesson" }),
    );
    await waitFor(() =>
      expect(container.querySelector("iframe")).not.toBeNull(),
    );
    const frame = container.querySelector("iframe")!;
    expect(frame.getAttribute("sandbox")).toBe("allow-scripts");
    expect(frame.getAttribute("referrerpolicy")).toBe("no-referrer");
    expect(frame.getAttribute("src")).toBe(url);
    fireEvent.click(screen.getByRole("button", { name: "Close practice" }));
    expect(container.querySelector("iframe")).toBeNull();
  });
  it("rejects launch addresses outside the capability file server", async () => {
    vi.mocked(api).mockResolvedValue({
      launchUrl: "https://example.test/lesson",
      title: "Untrusted",
    });
    const { container } = render(<ScormPlayer asset={asset} />);
    fireEvent.click(
      screen.getByRole("button", { name: "Start interactive lesson" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Invalid playback address",
    );
    expect(container.querySelector("iframe")).toBeNull();
  });
});

describe("anonymous SCORM 1.2 adapter", () => {
  it("implements lifecycle and errors, protects identity fields, and discards answers on finish", () => {
    const source = readFileSync(
      resolve(
        "../backend/src/MwabuLearn.Infrastructure/Content/scormAdapter.js",
      ),
      "utf8",
    );
    const window: Record<string, unknown> = {};
    runInNewContext(source, {
      window,
      document: { addEventListener() {} },
      HTMLScriptElement: class {},
    });
    const api = window.API as Record<string, (...args: string[]) => string>;
    expect(api.LMSGetValue("cmi.core.student_id")).toBe("");
    expect(api.LMSGetLastError()).toBe("301");
    expect(api.LMSInitialize("invalid")).toBe("false");
    expect(api.LMSInitialize("")).toBe("true");
    expect(api.LMSGetValue("cmi.core.student_id")).toBe("anonymous");
    expect(api.LMSSetValue("cmi.core.student_id", "learner-id")).toBe("false");
    expect(api.LMSGetLastError()).toBe("403");
    expect(api.LMSSetValue("cmi.suspend_data", "private practice answer")).toBe(
      "true",
    );
    expect(api.LMSGetValue("cmi.suspend_data")).toBe("private practice answer");
    expect(api.LMSSetValue("cmi.interactions.0.id", "fraction-question")).toBe(
      "true",
    );
    expect(api.LMSGetValue("cmi.interactions._count")).toBe("1");
    expect(api.LMSCommit("")).toBe("true");
    expect(api.LMSFinish("")).toBe("true");
    expect(api.LMSGetValue("cmi.suspend_data")).toBe("");
    expect(api.LMSGetLastError()).toBe("301");
    expect(source).not.toMatch(
      /localStorage|sessionStorage|indexedDB|fetch\(|XMLHttpRequest|postMessage/,
    );
  });
});
