import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import { useSession } from "../../auth/Session";
import { api, ApiError } from "../../api/client";
import ContinueTeaching from "./ContinueTeaching";
import { saveTeachingBookmark } from "./teachingBookmark";
vi.mock("../../auth/Session", () => ({ useSession: vi.fn() }));
vi.mock("../../api/client", async (original) => ({
  ...(await original<typeof import("../../api/client")>()),
  api: vi.fn(),
}));
afterEach(() => {
  sessionStorage.clear();
  vi.clearAllMocks();
});
function setup(allowed = true) {
  vi.mocked(useSession).mockReturnValue({
    organisationId: "org",
    user: { id: "teacher", email: "teacher@mwabu.invalid", firstName: "Teacher", lastName: "Test", isActive: true, createdAt: "2026-10-21T00:00:00Z" },
    can: () => allowed,
    memberships: [], setOrganisation: vi.fn(), loading: false, error: null, retry: vi.fn(),
  });
  saveTeachingBookmark("org", "teacher", {
    lessonId: "lesson",
    revision: 7,
    stepId: "two",
    assetId: "",
    updatedAt: Date.now(),
  });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(["continue-teaching", "org", "teacher", "lesson", 7], {
    lessonId: "lesson",
    revision: 7,
    title: "OLD PRIVATE TITLE",
    steps: [{ id: "two", title: "Development" }],
  });
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <ContinueTeaching />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
it("does not disclose a cached title when the pinned revision is no longer authorised", async () => {
  vi.mocked(api).mockRejectedValue(new ApiError(403, "Forbidden"));
  setup();
  expect(screen.queryByText(/OLD PRIVATE TITLE/)).toBeNull();
  expect(
    await screen.findByText(/Your saved lesson is unavailable/),
  ).toBeVisible();
  expect(screen.queryByRole("link")).toBeNull();
});
it("does not disclose a cached title or fetch when lesson permission is absent", () => {
  setup(false);
  expect(screen.queryByText(/OLD PRIVATE TITLE/)).toBeNull();
  expect(screen.queryByRole("link")).toBeNull();
  expect(api).not.toHaveBeenCalled();
});
it("opens only the server-authorised pinned revision", async () => {
  vi.mocked(api).mockResolvedValue({
    lessonId: "lesson",
    revision: 7,
    title: "Authorised fractions",
    steps: [{ id: "two", title: "Development" }],
  });
  setup();
  expect(
    await screen.findByRole("link", {
      name: "Continue teaching: Authorised fractions",
    }),
  ).toHaveAttribute("href", "/app/lessons/lesson/teach?revision=7&resume=true");
  expect(api).toHaveBeenCalledWith(
    expect.stringContaining("/lesson/classroom?revision=7"),
    expect.anything(),
  );
});
