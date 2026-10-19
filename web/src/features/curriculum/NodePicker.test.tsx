import { useState, type ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "../../api/client";
import NodePicker from "./NodePicker";
import LessonSetup from "../lessons/LessonSetup";
import { blankSetup } from "../lessons/types";

vi.mock("../../api/client", async () => ({
  ...(await vi.importActual<typeof import("../../api/client")>(
    "../../api/client",
  )),
  api: vi.fn(),
}));
const names = [
  "Zambia curriculum",
  "2026",
  "Grade 4",
  "Mathematics",
  "Term 1",
  "Fractions",
  "Recognise equal parts",
  "Represent one half",
];
const types = [
  "CurriculumVersion",
  "Grade",
  "Subject",
  "Term",
  "Topic",
  "Competency",
  "LearningOutcome",
];
const labels = [
  "Curriculum",
  "Versions",
  "Grades",
  "Subjects",
  "Terms",
  "Topics",
  "Competencies",
  "Learning outcomes",
];
const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider
    client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
  >
    {children}
  </QueryClientProvider>
);
beforeEach(() => {
  vi.mocked(api).mockReset();
  vi.mocked(api).mockImplementation(async (path) => {
    const level = types.findIndex((x) => path.includes(`/nodes/${x}?`)) + 1;
    return {
      items: [{ id: `node-${level}`, name: names[level], isActive: true }],
      hasMore: false,
    } as never;
  });
});
async function select(level: number) {
  const select = await screen.findByRole("combobox", {
    name: labels[level],
  });
  await waitFor(() => expect(select).toBeEnabled());
  await screen.findByRole("option", { name: names[level] });
  fireEvent.change(select, { target: { value: `node-${level}` } });
}
describe("teacher curriculum selection", () => {
  it("searches names on the server with active filtering and resets descendants when changing a parent", async () => {
    const reset = vi.fn();
    const choose = vi.fn();
    render(<NodePicker onSelect={choose} onReset={reset} />, { wrapper });
    fireEvent.change(
      screen.getByRole("searchbox", { name: "Search curriculum" }),
      { target: { value: "Zambia" } },
    );
    await waitFor(() =>
      expect(api).toHaveBeenCalledWith(
        expect.stringContaining("text=Zambia"),
        expect.anything(),
      ),
    );
    expect(vi.mocked(api).mock.calls.at(-1)?.[0]).toContain("activeOnly=true");
    for (let level = 0; level < 8; level++) await select(level);
    expect(choose).toHaveBeenLastCalledWith(
      "LearningOutcome",
      "node-7",
      names[7],
      names,
    );
    fireEvent.click(screen.getByRole("button", { name: "Grade 4 ›" }));
    expect(reset).toHaveBeenCalled();
    expect(
      screen.getByRole("combobox", { name: "Subjects" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Represent one half ›" }),
    ).not.toBeInTheDocument();
  });
  it("provides actionable empty results instead of an empty dropdown", async () => {
    vi.mocked(api).mockResolvedValue({ items: [], hasMore: false });
    render(<NodePicker onSelect={vi.fn()} />, { wrapper });
    expect(
      await screen.findByText(/Ask your curriculum administrator/),
    ).toHaveAttribute("role", "status");
  });
  it("derives a readable title and objectives without asking for internal codes", async () => {
    function Setup() {
      const [value, change] = useState(blankSetup());
      return <LessonSetup value={value} change={change} />;
    }
    render(<Setup />, { wrapper });
    for (let level = 0; level < 8; level++) await select(level);
    expect(screen.getByRole("textbox", { name: "Lesson title" })).toHaveValue(
      "Represent one half",
    );
    expect(screen.getByLabelText("Initial learning objectives")).toHaveValue(
      "Represent one half",
    );
    expect(
      screen.getByRole("combobox", { name: "Language of instruction" }),
    ).toHaveDisplayValue("English");
    fireEvent.click(screen.getByRole("button", { name: "Free-form" }));
    expect(screen.getByLabelText("Subject / learning area")).toHaveValue(
      "Mathematics",
    );
    expect(screen.getByLabelText("Target age / grade")).toHaveValue("Grade 4");
  });
  it("preserves a teacher's title and objectives when selecting curriculum", async () => {
    function Setup() {
      const [value, change] = useState({
        ...blankSetup(),
        title: "Paper folding",
        objectives: "Compare shapes",
      });
      return <LessonSetup value={value} change={change} />;
    }
    render(<Setup />, { wrapper });
    for (let level = 0; level < 8; level++) await select(level);
    expect(screen.getByRole("textbox", { name: "Lesson title" })).toHaveValue(
      "Paper folding",
    );
    expect(screen.getByLabelText("Initial learning objectives")).toHaveValue(
      "Compare shapes",
    );
  });
});
