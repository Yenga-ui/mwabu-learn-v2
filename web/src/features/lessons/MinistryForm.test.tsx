import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MinistryForm } from "./MinistryForm";
import {
  blankSections,
  blankSetup,
  type Detail,
  type LessonDocument,
} from "./types";
import { offlineLessonHtml } from "./offlineLesson";

const initial = (): LessonDocument => ({
  setup: blankSetup(),
  curriculum: null,
  references: [],
  sections: blankSections().map((x) =>
    x.code === "starter"
      ? { ...x, body: "3.2.1 Fold | compare", minutes: 5 }
      : x,
  ),
});
function Editor() {
  const [document, setDocument] = useState(initial());
  return (
    <MinistryForm
      document={document}
      disabled={false}
      update={(ministryForm) => setDocument({ ...document, ministryForm })}
    />
  );
}
describe("Ministry form integration", () => {
  it("upgrades legacy content without stripping syllabus codes or pipes and supports real teacher selections", () => {
    render(<Editor />);
    expect(screen.getByLabelText("Introduction: Teacher's role")).toHaveValue(
      "3.2.1 Fold | compare",
    );
    fireEvent.click(screen.getByLabelText("Collaboration"));
    expect(screen.getByLabelText("Collaboration")).toBeChecked();
    fireEvent.change(screen.getByLabelText("Introduction: Learners' role"), {
      target: { value: "Fold paper and compare two equal parts." },
    });
    expect(screen.getByLabelText("Introduction: Learners' role")).toHaveValue(
      "Fold paper and compare two equal parts.",
    );
    expect(
      screen.getByLabelText("Lesson evaluation (complete after teaching)"),
    ).toHaveValue("");
  });
  it("exports a self-contained escaped offline copy without executable content or requests", () => {
    const document = initial();
    document.setup.title = '<script>alert("test")</script>';
    const html = offlineLessonHtml(
      {
        document,
        authorName: "Synthetic teacher",
        viewedRevision: 3,
        lesson: { status: "Draft", revision: 3 },
      } as Detail,
      "Synthetic school",
    );
    expect(html).toContain("&lt;script&gt;");
    expect(html).not.toContain("<script>");
    expect(html).toContain("default-src 'none'");
    expect(html).toContain("revision 3");
    expect(html).toContain("Lesson progression");
    expect(html).toContain("3.2.1 Fold | compare");
  });
});
