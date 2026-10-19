import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LessonSlides } from "./LessonPresentation";
import { blankSetup, blankSections, type Detail } from "./types";
describe("classroom presentation", () => {
  it("presents saved sections with keyboard navigation and hides teaching notes until requested", () => {
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
    render(<LessonSlides detail={detail} />);
    expect(screen.getByText(/Saved revision 7/)).toBeVisible();
    expect(screen.getByText("Identify one half.")).toBeVisible();
    expect(screen.queryByText("Private teaching reminder.")).toBeNull();
    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.queryByRole("button", { name: "Save now" })).toBeNull();
    fireEvent.keyDown(
      screen.getByRole("region", { name: "Classroom presentation" }),
      { key: "ArrowRight" },
    );
    expect(screen.getByText("Compare two equal parts.")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Show teacher notes" }));
    expect(screen.getByText("Private teaching reminder.")).toBeVisible();
    expect(
      screen.getByRole("button", { name: "Hide teacher notes" }),
    ).toHaveAttribute("aria-expanded", "true");
  });
});
