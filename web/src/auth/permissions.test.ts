import { describe, expect, it } from "vitest";
import { can, safeReturn } from "./permissions";
import type { Access } from "../api/types";
const access: Access = {
  platformAuthority: false,
  platformPermissions: [],
  organisationPermissions: ["users.read"],
  cataloguePermissions: ["content.read"],
  globalPermissions: [],
  roleCodes: ["OrganisationAdmin"],
};
describe("permission boundaries", () => {
  it("keeps catalogue, scoped and platform grants separate rather than inferring grants from a role", () => {
    expect(can(access, "users.read")).toBe(true);
    expect(can(access, "users.read", "platform")).toBe(false);
    expect(can(access, "content.read", "catalogue")).toBe(true);
    expect(can(access, "content.manage", "global")).toBe(false);
    expect(can(undefined, "content.read")).toBe(false);
  });
  it.each([
    "https://attacker.invalid",
    "//attacker.invalid/app",
    "/app\\attacker",
    "/application",
    "/login",
    "javascript:alert(1)",
  ])("rejects unsafe return %s", (value) =>
    expect(safeReturn(value)).toBe("/app"),
  );
  it("preserves an internal resource route and filters", () =>
    expect(safeReturn("/app/resources?gradeId=123")).toBe(
      "/app/resources?gradeId=123",
    ));
});
