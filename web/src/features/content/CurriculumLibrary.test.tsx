import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, useLocation } from "react-router-dom";
import { api } from "../../api/client";
import CurriculumLibrary from "./CurriculumLibrary";
vi.mock("../../api/client", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../api/client")>()),
  api: vi.fn(),
}));
vi.mock("../../auth/Session", () => ({
  useSession: () => ({ can: () => true, organisationId: "school" }),
}));
const choices = [
  {
    year: 2023,
    grade: 4,
    subject: "Mathematics",
    topic: "Fractions",
    term: null,
  },
  { year: 2023, grade: 4, subject: "English", topic: "Reading", term: null },
  {
    year: 2013,
    grade: 3,
    subject: "Mathematics",
    topic: "Fractions",
    term: null,
  },
];
function Location() {
  return <output aria-label="Current filters">{useLocation().search}</output>;
}
function show(path: string) {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter initialEntries={[path]}>
        <CurriculumLibrary />
        <Location />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
beforeEach(() => {
  vi.mocked(api).mockReset();
  vi.mocked(api).mockResolvedValue({
    resources: { items: [], totalCount: 0, page: 1, pageSize: 24 },
    choices,
  });
});
it("changing curriculum year clears dependent choices while retaining the keyword search", async () => {
  show(
    "/app/library?year=2023&grade=4&subject=Mathematics&topic=Fractions&term=Term+1&text=equal",
  );
  await waitFor(() =>
    expect(
      screen.getByRole("option", { name: "2013 Zambian curriculum" }),
    ).toBeInTheDocument(),
  );
  fireEvent.change(
    screen.getByRole("combobox", {
      name: "Curriculum collection",
    }),
    { target: { value: "2013" } },
  );
  await waitFor(() =>
    expect(screen.getByLabelText("Current filters")).toHaveTextContent(
      "?year=2013&text=equal",
    ),
  );
  expect(screen.getByRole("combobox", { name: "Grade" })).toHaveValue("");
  expect(screen.getByRole("combobox", { name: "Topic" })).toHaveValue("");
});
it("subject choices retain siblings and a linked legacy resource is not relabelled as a 2023 source plan", async () => {
  vi.mocked(api).mockResolvedValue({
    choices,
    resources: {
      items: [
        {
          content: {
            id: "legacy",
            title: "Legacy fractions",
            contentType: "interactive-activity",
            summary: "Original source",
          },
          year: null,
          grade: null,
          subject: null,
          topic: null,
          term: null,
          isTemplate: false,
          associatedResource: true,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 24,
    },
  });
  show("/app/library?year=2023&grade=4&subject=Mathematics&topic=Fractions");
  expect(
    await screen.findByRole("link", { name: "Legacy fractions" }),
  ).toHaveAttribute("href", expect.stringContaining("return="));
  expect(screen.getByRole("option", { name: "English" })).toBeInTheDocument();
  expect(
    screen.getByText(
      "Reviewed resource associated with this teaching context.",
    ),
  ).toBeInTheDocument();
  expect(screen.queryByText("Mwabu lesson plan")).not.toBeInTheDocument();
});
it("homepage search includes approved organisation plans through the scoped API without exposing private drafts", async () => {
  vi.mocked(api).mockImplementation(async (path) =>
    path.startsWith("/api/organisations/")
      ? {
          items: [
            {
              id: "approved-plan",
              title: "School fractions",
              grade: "Grade 4",
              subject: "Mathematics",
              resourceCount: 1,
            },
          ],
          hasMore: false,
        }
      : {
          choices,
          resources: { items: [], totalCount: 0, page: 1, pageSize: 24 },
        },
  );
  show("/app/library?text=fractions");
  expect(
    await screen.findByRole("link", { name: "School fractions" }),
  ).toHaveAttribute("href", "/app/lessons/approved-plan");
  expect(screen.getByText("1 learning resources attached")).toBeInTheDocument();
  expect(
    vi
      .mocked(api)
      .mock.calls.some(
        ([path]) =>
          path ===
          "/api/organisations/school/lessons?status=Published&pageSize=12&text=fractions",
      ),
  ).toBe(true);
});
