import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LessonSlides } from "./LessonPresentation";
import { blankSetup, blankSections, type Detail } from "./types";
import { MemoryRouter } from "react-router-dom";
describe("classroom presentation", () => {
  it("keeps the legacy presentation entry point pinned and projection-safe", () => {
    const detail = {
      authorName: "Teacher",
      canEdit: true,
      canReview: false,
      viewedRevision: 7,
      lesson: {
        id: "plan",
        status: "Draft",
        organisationId: "org",
        ownerUserId: "teacher",
        title: "Equal shares",
        mode: "FreeForm",
        revision: 7,
        isOrganisationTemplate: false,
        createdAt: "2026-10-19T00:00:00Z",
        updatedAt: null,
      },
      document: {
        teachingSteps: [
          {
            id: "objectives",
            sectionCode: "objectives",
            title: "Learning objectives",
            phase: "Introduction",
            minutes: 0,
            learnerExplanation: "Identify one half.",
            teacherInstructions: "",
            teacherNotes: "Private teaching reminder.",
            hint: "",
            answer: "",
            resources: [],
          },
          {
            id: "starter",
            sectionCode: "starter",
            title: "Discussion",
            phase: "Discussion",
            minutes: 5,
            learnerExplanation: "Compare two equal parts.",
            teacherInstructions: "",
            teacherNotes: "",
            hint: "",
            answer: "",
            resources: [],
          },
        ],
        setup: { ...blankSetup(), title: "Equal shares" },
        references: [],
        curriculum: null,
        sections: blankSections().map((x) => ({
          ...x,
          body:
            x.code === "objectives"
              ? "Identify one half."
              : x.code === "starter"
                ? "Compare two equal parts."
                : x.code === "teacher-activities"
                  ? "Private teaching reminder."
                  : "",
        })),
      },
    } as Detail;
    render(
      <MemoryRouter>
        <LessonSlides detail={detail} />
      </MemoryRouter>,
    );
    expect(screen.getByText(/Saved revision 7/)).toBeVisible();
    expect(screen.getByText("Identify one half.")).toBeVisible();
    expect(screen.queryByText("Private teaching reminder.")).toBeNull();
    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.queryByRole("button", { name: "Save now" })).toBeNull();
    fireEvent.keyDown(
      screen.getByRole("region", { name: "Classroom teaching" }),
      { key: "ArrowRight" },
    );
    expect(screen.getByText("Compare two equal parts.")).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Show teacher notes" }),
    ).toBeNull();
    expect(screen.queryByText("Private teaching reminder.")).toBeNull();
  });
});
