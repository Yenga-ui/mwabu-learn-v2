import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { expect, it, vi } from "vitest";
import TeacherCurriculum from "./TeacherCurriculum";
import { api } from "../../api/client";
vi.mock("../../api/client", async (original) => ({
  ...(await original<typeof import("../../api/client")>()),
  api: vi.fn(),
}));
it("browses real grade, subject, term, topic and outcomes without requiring technical codes", async () => {
  vi.mocked(api).mockImplementation(async (path) => {
    const url = String(path);
    const items = url.includes("grades/search")
      ? [
          {
            id: "g",
            name: "Grade 4",
            curriculumId: "c",
            versionId: "v",
            versionName: "2023",
            curriculumName: "Zambian syllabus",
          },
        ]
      : url.includes("/Subject?")
        ? [{ id: "s", name: "Mathematics" }]
        : url.includes("/Term?")
          ? [{ id: "t", name: "Term 1" }]
          : url.includes("/Topic?")
            ? [{ id: "topic", name: "Fractions" }]
            : url.includes("/Competency?")
              ? [{ id: "comp", name: "Equal parts", code: "REF-SECRET" }]
              : [{ id: "outcome", name: "Represent one half" }];
    return { items, page: 1, pageSize: 30, hasMore: false } as never;
  });
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter>
        <TeacherCurriculum />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  fireEvent.click(await screen.findByRole("button", { name: "Grade 4" }));
  fireEvent.click(await screen.findByRole("button", { name: "Mathematics" }));
  fireEvent.change(await screen.findByLabelText("Term / teaching sequence"), {
    target: { value: "t" },
  });
  fireEvent.click(await screen.findByRole("button", { name: "Fractions" }));
  expect(await screen.findByText("Represent one half")).toBeInTheDocument();
  expect(
    screen.getByRole("link", { name: /Find linked lesson plans/ }),
  ).toHaveAttribute(
    "href",
    "/app/lessons?nodeType=LearningOutcome&nodeId=outcome&context=Represent%20one%20half",
  );
  expect(screen.getByText("REF-SECRET").closest("details")).not.toHaveAttribute(
    "open",
  );
  fireEvent.click(screen.getByRole("button", { name: "Grade 4" }));
  expect(screen.queryByText("Represent one half")).not.toBeInTheDocument();
});
it("offers published topics when no managed term exists without inventing alignment", async () => {
  vi.mocked(api).mockImplementation(async (path) => {
    const url = String(path);
    if (url.includes("/api/learning/library"))
      return {
        choices: [
          {
            year: 2023,
            grade: 4,
            subject: "Mathematics",
            topic: "Fractions",
            term: null,
          },
        ],
      } as never;
    const items = url.includes("grades/search")
      ? [
          {
            id: "g",
            name: "Grade 4",
            curriculumId: "c",
            versionId: "v",
            versionName: "2023",
            curriculumName: "Zambian syllabus",
          },
        ]
      : url.includes("/Subject?")
        ? [{ id: "s", name: "Mathematics" }]
        : [];
    return { items, page: 1, pageSize: 30, hasMore: false } as never;
  });
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter>
        <TeacherCurriculum />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  fireEvent.click(await screen.findByRole("button", { name: "Grade 4" }));
  fireEvent.click(await screen.findByRole("button", { name: "Mathematics" }));
  expect(
    await screen.findByRole("heading", { name: "Fractions" }),
  ).toBeInTheDocument();
  expect(
    screen.getByRole("link", { name: "View lesson plans and resources →" }),
  ).toHaveAttribute(
    "href",
    "/app/library?year=2023&grade=4&subject=Mathematics&topic=Fractions",
  );
  expect(screen.getByText(/Term not recorded/)).toBeInTheDocument();
});
