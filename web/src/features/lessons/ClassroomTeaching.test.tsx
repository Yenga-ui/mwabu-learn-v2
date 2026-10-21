import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { ClassroomCanvas } from "./ClassroomTeaching";
import { blankSetup, type Detail, type TeachingStep } from "./types";
describe("projection-safe classroom teaching", () => {
  it("resumes only a matching revision and step with hidden answers, and clears on finish", () => {
    const detail = {
      lessonId: "lesson",
      revision: 7,
      title: "Fractions",
      language: "en",
      steps: [
        {
          id: "one",
          title: "Introduction",
          phase: "Introduction",
          minutes: 5,
          learnerExplanation: "Start here",
          hint: "hint",
          answer: "answer",
          resources: [],
        },
        {
          id: "two",
          title: "Conclusion",
          phase: "Recap",
          minutes: 5,
          learnerExplanation: "",
          hint: "hint",
          answer: "answer",
          resources: [],
        },
      ],
    };
    const finish = vi.fn();
    const position = vi.fn();
    const view = render(
      <MemoryRouter>
        <ClassroomCanvas
          detail={detail}
          bookmark={{
            lessonId: "lesson",
            revision: 7,
            stepId: "two",
            assetId: "not-authorised",
            updatedAt: Date.now(),
          }}
          onPosition={position}
          onFinish={finish}
        />
      </MemoryRouter>,
    );
    expect(screen.getByRole("heading", { name: "Conclusion" })).toBeVisible();
    expect(screen.getByText(/What did you learn today/)).toBeVisible();
    expect(screen.queryByText("Answer: answer")).toBeNull();
    expect(position).toHaveBeenCalledWith("two", "");
    fireEvent.click(screen.getByRole("link", { name: "Finish lesson" }));
    expect(finish).toHaveBeenCalledOnce();
    view.unmount();
    render(
      <MemoryRouter>
        <ClassroomCanvas
          detail={detail}
          bookmark={{
            lessonId: "lesson",
            revision: 6,
            stepId: "two",
            assetId: "",
            updatedAt: Date.now(),
          }}
        />
      </MemoryRouter>,
    );
    expect(screen.getByRole("heading", { name: "Introduction" })).toBeVisible();
  });
  it("pins revision, hides all private guidance, reveals only on demand and resets on navigation", () => {
    const step: TeachingStep = {
      id: "one",
      sectionCode: "starter",
      title: "Equal parts",
      phase: "Discussion",
      minutes: 5,
      learnerExplanation: "Compare these parts",
      teacherInstructions: "Private instruction",
      teacherNotes: "Confidential note",
      hint: "Compare their size",
      answer: "Both parts are equal",
      resources: [],
    };
    const detail = {
      viewedRevision: 7,
      lesson: { id: "lesson" },
      document: {
        setup: { ...blankSetup(), title: "Fractions" },
        sections: [],
        references: [],
        teachingSteps: [
          step,
          {
            ...step,
            id: "two",
            title: "Guided practice",
            learnerExplanation: "Try another example",
          },
        ],
      },
    } as unknown as Detail;
    render(
      <MemoryRouter>
        <ClassroomCanvas detail={detail} />
      </MemoryRouter>,
    );
    expect(screen.getByText(/Saved revision 7/)).toBeVisible();
    expect(screen.queryByText("Private instruction")).toBeNull();
    expect(screen.queryByText("Confidential note")).toBeNull();
    expect(screen.queryByText(/Hint: Compare their size/)).toBeNull();
    expect(screen.queryByText(/Answer: Both parts/)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Show hint" }));
    expect(screen.getByText("Hint: Compare their size")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Reveal answer" }));
    expect(screen.getByText("Answer: Both parts are equal")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Next step" }));
    expect(screen.getByText("Try another example")).toBeVisible();
    expect(screen.queryByText("Hint: Compare their size")).toBeNull();
    expect(screen.queryByText("Answer: Both parts are equal")).toBeNull();
    expect(
      screen.queryByRole("button", { name: "Show teacher notes" }),
    ).toBeNull();
    expect(
      screen.getByRole("link", { name: "Exit to preparation" }),
    ).toHaveAttribute("href", "/app/lessons/lesson/journey?revision=7");
  });
});
