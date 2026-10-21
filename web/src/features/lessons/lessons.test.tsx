import { useState, type ReactNode } from "react";
import {
  act,
  fireEvent,
  render,
  renderHook,
  screen,
  waitFor,
} from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { api, ApiError } from "../../api/client";
import SetupForm from "./LessonSetup";
import SourceExplorer from "./SourceExplorer";
import { DocumentComparison } from "./RevisionPanel";
import { LessonPrint } from "./LessonPrint";
import { useLessonDraft } from "./useLessonDraft";
import {
  authoritative,
  blankSections,
  blankSetup,
  proposalDocument,
  type Detail,
  type Generation,
  type Source,
} from "./types";
vi.mock("../../api/client", async () => ({
  ...(await vi.importActual<typeof import("../../api/client")>(
    "../../api/client",
  )),
  api: vi.fn(),
  upload: vi.fn(),
}));
const initial = (): Detail => ({
  authorName: "Teacher Test",
  canEdit: true,
  canReview: false,
  viewedRevision: 1,
  lesson: {
    id: "lesson-1",
    organisationId: "org-1",
    ownerUserId: "teacher-1",
    title: "Equal shares",
    mode: "FreeForm",
    status: "Draft",
    revision: 1,
    isOrganisationTemplate: false,
    createdAt: "2026-10-13T00:00:00Z",
    updatedAt: null,
  },
  document: {
    setup: {
      ...blankSetup(),
      mode: "FreeForm",
      title: "Equal shares",
      subject: "Maths",
      targetLearners: "Age 10",
    },
    curriculum: null,
    sections: blankSections(),
    references: [],
  },
});
beforeEach(() => {
  vi.mocked(api).mockReset();
});
describe("lesson structure and setup", () => {
  it("supports all professionally defined sections with protected official fields", () => {
    const sections = blankSections();
    expect(sections).toHaveLength(28);
    expect(new Set(sections.map((x) => x.code)).size).toBe(28);
    expect(authoritative).toEqual([
      "curriculum",
      "competencies",
      "learning-outcomes",
    ]);
  });
  it("switches planning modes without losing objectives, language or subject", () => {
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false } } })
        }
      >
        {children}
      </QueryClientProvider>
    );
    vi.mocked(api).mockResolvedValue({ items: [], hasMore: false });
    function Setup() {
      const [value, setValue] = useState({
        ...blankSetup(),
        mode: "FreeForm" as const,
        objectives: "Explain equal halves",
        subject: "Mathematics",
        language: "bem",
      });
      return (
        <SetupForm
          value={value}
          change={(next) => setValue(next as typeof value)}
        />
      );
    }
    render(<Setup />, { wrapper });
    fireEvent.click(screen.getByRole("button", { name: "Curriculum-aligned" }));
    expect(screen.getByLabelText("Initial learning objectives")).toHaveValue(
      "Explain equal halves",
    );
    expect(screen.getByLabelText("Language of instruction")).toHaveValue("bem");
    fireEvent.click(screen.getByRole("button", { name: "Free-form" }));
    expect(screen.getByLabelText("Subject / learning area")).toHaveValue(
      "Mathematics",
    );
  });
  it("merges a section proposal without overwriting other teacher sections", () => {
    const document = initial().document;
    document.sections[0].body = "Teacher-owned title wording";
    const generation: Generation = {
      id: "generation-1",
      state: "completed",
      baseRevision: 1,
      operation: "simplify",
      provider: "test",
      model: "test",
      estimatedCostUsd: 0,
      usageEstimated: false,
      errorCode: null,
      createdAt: "2026-10-13T00:00:00Z",
      sectionCode: "objectives",
      result: {
        explanation: "Simplify objectives",
        sections: [
          {
            code: "objectives",
            title: "Objectives",
            body: "Explain equal shares",
            minutes: 0,
            sourceIds: [],
          },
        ],
      },
    };
    const proposal = proposalDocument(document, generation);
    expect(proposal.sections[0].body).toBe("Teacher-owned title wording");
    expect(proposal.sections.find((x) => x.code === "objectives")?.body).toBe(
      "Explain equal shares",
    );
    expect(document.sections.find((x) => x.code === "objectives")?.body).toBe(
      "",
    );
  });
  it("keeps the current document when a failed generation has no proposal", () => {
    const document = initial().document;
    expect(proposalDocument(document, { result: null } as Generation)).toBe(
      document,
    );
  });
  it("shows revision changes as escaped before/after content including citation changes", () => {
    const before = initial().document;
    const after = {
      ...before,
      sections: before.sections.map((x) =>
        x.code === "objectives"
          ? {
              ...x,
              body: "<script>instruction-bearing text</script>",
              sourceIds: ["source-1"],
            }
          : x,
      ),
    };
    const { container } = render(
      <DocumentComparison before={before} after={after} />,
    );
    expect(
      screen.getByText("<script>instruction-bearing text</script>"),
    ).toBeVisible();
    expect(container.querySelector("script")).toBeNull();
    expect(screen.getByText("Before")).toBeVisible();
  });
  it("print preview reflects the saved teacher wording and flags unsaved copies", () => {
    const document = initial().document;
    document.sections.find((x) => x.code === "objectives")!.body =
      "Teacher approved exact wording";
    render(
      <LessonPrint
        document={document}
        organisation="School A"
        teacher="Teacher A"
        revision={3}
        status="Published"
        dirty={false}
      />,
    );
    expect(screen.getByText("Teacher approved exact wording")).toBeVisible();
    expect(screen.getByText(/School A.*Teacher A.*revision 3/)).toBeVisible();
    expect(screen.queryByText(/UNSAVED PREVIEW/)).toBeNull();
  });
  it("unsaved print previews cannot be mistaken for an approved saved export", () => {
    render(
      <LessonPrint
        document={initial().document}
        organisation="School"
        teacher="Teacher"
        revision={1}
        status="Draft"
        dirty
      />,
    );
    expect(screen.getByText(/UNSAVED PREVIEW/)).toBeVisible();
  });
});
describe("safe autosave", () => {
  it("does not repopulate private cached data when a save completes after closing or signing out", async () => {
    const value = initial();
    let complete!: (result: Detail) => void;
    vi.mocked(api).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          complete = resolve;
        }),
    );
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const hook = renderHook(() => useLessonDraft(value, "org-1"), { wrapper });
    act(() =>
      hook.result.current.update((document) => ({
        ...document,
        setup: { ...document.setup, title: "Private teacher edit" },
      })),
    );
    let pending!: Promise<Detail>;
    act(() => {
      pending = hook.result.current.save();
    });
    hook.unmount();
    client.clear();
    await act(async () => {
      complete({ ...value, lesson: { ...value.lesson, revision: 2 } });
      await pending;
    });
    expect(
      client.getQueryData(["lesson", "org-1", value.lesson.id]),
    ).toBeUndefined();
    expect(client.getQueryCache().getAll()).toHaveLength(0);
  });
  it("saves with the last server revision and adopts the authoritative saved document", async () => {
    const value = initial();
    const response = {
      ...value,
      lesson: { ...value.lesson, revision: 2 },
      document: {
        ...value.document,
        setup: { ...value.document.setup, title: "Edited title" },
      },
    };
    vi.mocked(api).mockResolvedValue(response);
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const hook = renderHook(() => useLessonDraft(value, "org-1"), { wrapper });
    act(() =>
      hook.result.current.update((document) => ({
        ...document,
        setup: { ...document.setup, title: "Edited title" },
      })),
    );
    await act(async () => {
      await hook.result.current.save();
    });
    expect(vi.mocked(api).mock.calls[0][1]?.body).toMatchObject({
      expectedRevision: 1,
    });
    expect(hook.result.current.dirty).toBe(false);
    expect(hook.result.current.server.lesson.revision).toBe(2);
    expect(
      client.getQueryData<Detail>(["lesson", "org-1", value.lesson.id])?.lesson
        .revision,
    ).toBe(2);
  });
  it("preserves edits made while a save is in flight", async () => {
    const value = initial();
    let complete!: (value: Detail) => void;
    vi.mocked(api).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          complete = resolve;
        }),
    );
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const hook = renderHook(() => useLessonDraft(value, "org-1"), { wrapper });
    act(() =>
      hook.result.current.update((document) => ({
        ...document,
        setup: { ...document.setup, title: "First edit" },
      })),
    );
    let pending!: Promise<Detail>;
    act(() => {
      pending = hook.result.current.save();
    });
    act(() =>
      hook.result.current.update((document) => ({
        ...document,
        setup: { ...document.setup, title: "Newer unsaved edit" },
      })),
    );
    await act(async () => {
      complete({
        ...value,
        lesson: { ...value.lesson, revision: 2 },
        document: {
          ...value.document,
          setup: { ...value.document.setup, title: "First edit" },
        },
      });
      await pending;
    });
    expect(hook.result.current.document.setup.title).toBe("Newer unsaved edit");
    expect(hook.result.current.dirty).toBe(true);
    expect(hook.result.current.server.lesson.revision).toBe(2);
    expect(
      client.getQueryData<Detail>(["lesson", "org-1", value.lesson.id])?.lesson
        .revision,
    ).toBe(2);
  });
  it("pauses autosave on conflict and keeps the teacher's local work", async () => {
    const value = initial();
    vi.mocked(api).mockRejectedValue(new ApiError(409, "Changed concurrently"));
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const hook = renderHook(() => useLessonDraft(value, "org-1"), { wrapper });
    act(() =>
      hook.result.current.update((document) => ({
        ...document,
        setup: { ...document.setup, title: "Keep my manual work" },
      })),
    );
    let caught: unknown;
    let pending: Promise<Detail>;
    act(() => {
      pending = hook.result.current.save();
    });
    try {
      await pending!;
    } catch (failure) {
      caught = failure;
    }
    await act(async () => {
      await Promise.resolve();
    });
    expect(caught).toBeInstanceOf(ApiError);
    expect(hook.result.current.document.setup.title).toBe(
      "Keep my manual work",
    );
    expect(hook.result.current.dirty).toBe(true);
    expect(hook.result.current.conflict).toBe(true);
    expect(hook.result.current.blocked).toBe(true);
  });
});
describe("source selection", () => {
  it("captures checkbox choice before awaiting save and labels metadata without claiming a read", async () => {
    const source = {
      id: "source-1",
      kind: "TeacherDocument",
      title: "Reference document",
      retrievalStatus: "MetadataOnly",
      evidenceDescription: "Metadata only; contents not read",
      credibility: "Teacher reference",
      isSelected: false,
      isExcluded: false,
      priority: 0,
      reusePermitted: false,
      evidencePreview: "",
      licence: null,
      url: null,
    } as Source;
    vi.mocked(api).mockImplementation(async (_path, options) =>
      options?.method === "PATCH" ? { ...source, isSelected: true } : [source],
    );
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const changed = vi.fn();
    render(
      <QueryClientProvider client={client}>
        <SourceExplorer
          path="/api/organisations/org/lessons/plan"
          title="Lesson"
          language="en"
          editable
          prepare={async () => {
            await Promise.resolve();
          }}
          changed={changed}
        />
      </QueryClientProvider>,
    );
    await waitFor(() =>
      expect(
        screen.getByText("Metadata only; contents not read"),
      ).toBeVisible(),
    );
    fireEvent.click(
      screen.getByLabelText("Use Reference document as a reference"),
    );
    await waitFor(() => expect(changed).toHaveBeenCalled());
    const call = vi
      .mocked(api)
      .mock.calls.find(([, options]) => options?.method === "PATCH");
    expect(call?.[1]?.body).toMatchObject({ isSelected: true });
  });
});
