import { it, expect, vi, afterEach } from "vitest";
import { webcrypto } from "node:crypto";
import { lessonPackageHtml, sha256, type LessonPackage } from "./lessonPackage";
afterEach(() => vi.unstubAllGlobals());
it("exports only complete hash-verified media packages with escaped text and a fixed script", async () => {
  vi.stubGlobal("crypto", webcrypto);
  const bytes = new TextEncoder().encode("approved media");
  const checksum = await sha256(bytes);
  const manifest: LessonPackage = {
    schemaVersion: 1,
    lessonId: "lesson",
    revision: 3,
    title: "<script>bad</script>",
    language: "en",
    sections: [{ title: "Objective", body: "Represent one half" }],
    version: "version-3",
    assets: [
      {
        id: "asset",
        title: "Teaching resource",
        mimeType: "application/pdf",
        size: bytes.length,
        checksum,
        downloadUrl: "/private-download",
      },
    ],
  };
  const html = await lessonPackageHtml(manifest, [bytes]);
  expect(html).toContain("script-src 'sha256-");
  expect(html).toContain("\\u003cscript>");
  expect(html).not.toContain("/private-download");
  expect(html).not.toContain("fetch(");
  expect(html).not.toContain("localStorage");
  await expect(
    lessonPackageHtml(manifest, [new Uint8Array([1])]),
  ).rejects.toThrow("integrity");
  await expect(lessonPackageHtml(manifest, [])).rejects.toThrow("incomplete");
});
it("exports public teaching fields only and rejects unknown package versions", async () => {
  vi.stubGlobal("crypto", webcrypto);
  const manifest = {
    schemaVersion: 2,
    lessonId: "lesson",
    revision: 4,
    title: "Fractions",
    language: "en",
    sections: [],
    assets: [],
    version: "version-4",
    teacherNotes: "PRIVATE ROOT",
    teachingSteps: [
      {
        id: "intro",
        title: "Equal parts",
        phase: "Discussion",
        minutes: 5,
        learnerExplanation: "Compare equal parts",
        hint: "Compare sizes",
        answer: "Equal",
        resources: [],
        teacherNotes: "PRIVATE STEP",
        teacherInstructions: "PRIVATE GUIDANCE",
      },
    ],
  };
  const html = await lessonPackageHtml(manifest, []);
  expect(html).not.toContain("PRIVATE");
  expect(html).not.toContain("teacherNotes");
  expect(html).toContain("Compare equal parts");
  await expect(
    lessonPackageHtml({ ...manifest, schemaVersion: 99 }, []),
  ).rejects.toThrow("Unsupported");
});
