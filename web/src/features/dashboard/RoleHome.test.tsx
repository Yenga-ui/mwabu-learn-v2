import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, expect, it, vi } from "vitest";
import { AdminHome, workspaceKind } from "./RoleHome";
const allowed = new Set<string>();
vi.mock("../../auth/Session", () => ({
  useSession: () => ({
    organisationId: "school-one",
    user: { firstName: "Test" },
    memberships: [],
    access: { platformAuthority: false },
    can: (permission: string) => allowed.has(permission),
  }),
}));
beforeEach(() => allowed.clear());
it("prioritises administration over a combined teacher role and retains teacher/learner separation", () => {
  expect(
    workspaceKind({ platformAuthority: true, roleCodes: ["Teacher"] }),
  ).toBe("platform");
  expect(
    workspaceKind({
      platformAuthority: false,
      roleCodes: ["OrganisationAdmin", "Teacher"],
    }),
  ).toBe("organisation");
  expect(
    workspaceKind({
      platformAuthority: false,
      roleCodes: ["Teacher", "Learner"],
    }),
  ).toBe("other");
  expect(
    workspaceKind({ platformAuthority: false, roleCodes: ["Learner"] }),
  ).toBe("learner");
});
it("keeps organisation actions scoped and does not offer platform licence controls", () => {
  allowed.add("organisations.read");
  allowed.add("memberships.manage");
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <AdminHome platform={false} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  expect(
    screen.getByRole("link", { name: /People in your organisation/ }),
  ).toHaveAttribute("href", "/app/organisations/school-one#members");
  expect(
    screen.getByRole("link", { name: /Assigned curriculum/ }),
  ).toHaveAttribute(
    "href",
    "/app/organisations/school-one#assigned-curriculum",
  );
  expect(
    screen.queryByRole("link", { name: /People & licences/ }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole("link", { name: /Audit trail/ }),
  ).not.toBeInTheDocument();
});
