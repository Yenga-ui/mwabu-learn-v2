import type { Access } from "../api/types";
export type Scope = "platform" | "organisation" | "catalogue" | "global";
export function can(
  access: Access | undefined,
  code: string,
  scope: Scope = "organisation",
): boolean {
  if (!access) return false;
  return (
    scope === "platform"
      ? access.platformPermissions
      : scope === "catalogue"
        ? access.cataloguePermissions
        : scope === "global"
          ? access.globalPermissions
          : access.organisationPermissions
  ).includes(code);
}
export function safeReturn(value: string | null): string {
  if (
    !value ||
    !value.startsWith("/app") ||
    value.startsWith("//") ||
    value.includes("\\")
  )
    return "/app";
  const url = new URL(value, "https://mwabu.invalid");
  return url.origin === "https://mwabu.invalid" &&
    (url.pathname === "/app" || url.pathname.startsWith("/app/"))
    ? url.pathname + url.search
    : "/app";
}
