// Teacher navigation only: no lesson text, learner identities, answers or SCORM state.
// Session storage is intentionally not synchronised or retained across sign-out.
const prefix = "mwabu:teaching-position:";
const lifetime = 12 * 60 * 60 * 1000;
export interface TeachingBookmark {
  lessonId: string;
  revision: number;
  stepId: string;
  assetId: string;
  updatedAt: number;
}
const id = (value: unknown): value is string =>
  typeof value === "string" && /^[a-zA-Z0-9_-]{1,100}$/.test(value);
export function readTeachingBookmark(
  org: string,
  user: string,
): TeachingBookmark | undefined {
  try {
    const value = JSON.parse(
      sessionStorage.getItem(prefix + org + ":" + user) ?? "null",
    );
    if (
      !value ||
      !id(value.lessonId) ||
      !id(value.stepId) ||
      !(value.assetId === "" || id(value.assetId)) ||
      !Number.isInteger(value.revision) ||
      value.revision < 1 ||
      !Number.isFinite(value.updatedAt) ||
      value.updatedAt > Date.now() ||
      Date.now() - value.updatedAt > lifetime
    )
      return undefined;
    return {
      lessonId: value.lessonId,
      revision: value.revision,
      stepId: value.stepId,
      assetId: value.assetId,
      updatedAt: value.updatedAt,
    };
  } catch {
    return undefined;
  }
}
export function saveTeachingBookmark(
  org: string,
  user: string,
  position: TeachingBookmark,
) {
  try {
    sessionStorage.setItem(
      prefix + org + ":" + user,
      JSON.stringify({
        lessonId: position.lessonId,
        revision: position.revision,
        stepId: position.stepId,
        assetId: position.assetId,
        updatedAt: position.updatedAt,
      }),
    );
  } catch {
    /* Teaching remains usable when storage is unavailable. */
  }
}
export function forgetTeachingBookmark(org: string, user: string) {
  try {
    sessionStorage.removeItem(prefix + org + ":" + user);
  } catch {
    /* Optional navigation state. */
  }
}
export function clearTeachingBookmarks() {
  try {
    for (let index = sessionStorage.length - 1; index >= 0; index--) {
      const key = sessionStorage.key(index);
      if (key?.startsWith(prefix)) sessionStorage.removeItem(key);
    }
  } catch {
    /* Optional navigation state. */
  }
}
