import { afterEach, expect, it, vi } from "vitest";
import {
  readTeachingBookmark,
  saveTeachingBookmark,
  clearTeachingBookmarks,
  forgetTeachingBookmark,
} from "./teachingBookmark";
afterEach(() => {
  sessionStorage.clear();
  vi.useRealTimers();
});
it("keeps only teacher navigation IDs, scopes to user and organisation, and clears on finish/sign-out", () => {
  const position = {
    lessonId: "lesson",
    revision: 7,
    stepId: "step-two",
    assetId: "asset",
    updatedAt: Date.now(),
  };
  saveTeachingBookmark("school", "teacher", {
    ...position,
    answer: "PRIVATE",
  } as typeof position);
  expect(readTeachingBookmark("school", "teacher")).toEqual(position);
  expect(readTeachingBookmark("another-school", "teacher")).toBeUndefined();
  expect(readTeachingBookmark("school", "another-teacher")).toBeUndefined();
  expect(
    sessionStorage.getItem("mwabu:teaching-position:school:teacher"),
  ).not.toContain("PRIVATE");
  forgetTeachingBookmark("school", "teacher");
  expect(readTeachingBookmark("school", "teacher")).toBeUndefined();
  saveTeachingBookmark("school", "teacher", position);
  sessionStorage.setItem("unrelated", "preserve");
  clearTeachingBookmarks();
  expect(readTeachingBookmark("school", "teacher")).toBeUndefined();
  expect(sessionStorage.getItem("unrelated")).toBe("preserve");
});
it("rejects expired, malformed, future and invalid revision bookmarks", () => {
  vi.useFakeTimers();
  const position = {
    lessonId: "lesson",
    revision: 7,
    stepId: "step-two",
    assetId: "",
    updatedAt: Date.now(),
  };
  saveTeachingBookmark("school", "teacher", position);
  vi.advanceTimersByTime(12 * 60 * 60 * 1000 + 1);
  expect(readTeachingBookmark("school", "teacher")).toBeUndefined();
  for (const invalid of [
    "broken JSON",
    JSON.stringify({ ...position, revision: -1 }),
    JSON.stringify({ ...position, updatedAt: Date.now() + 10000 }),
  ]) {
    sessionStorage.setItem("mwabu:teaching-position:school:teacher", invalid);
    expect(readTeachingBookmark("school", "teacher")).toBeUndefined();
  }
});
