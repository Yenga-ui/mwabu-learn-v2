import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import OfficialPlanEditor from "./OfficialPlanEditor";
import { blankSetup, blankSections, type LessonDocument } from "./types";
import { recentLessonIds, rememberLesson } from "./RecentLessons";

function Editor() {
  const [document, setDocument] = useState<LessonDocument>({
    setup: blankSetup(),
    curriculum: null,
    sections: blankSections(),
    references: [],
  });
  return (
    <>
      <OfficialPlanEditor
        document={document}
        disabled={false}
        update={setDocument}
      />
      <output data-testid="saved-draft">{JSON.stringify(document)}</output>
    </>
  );
}
it("a teacher completes official fields, selections and all seven stages without internal identifiers", () => {
  render(<Editor />);
  fireEvent.change(screen.getByLabelText("School"), {
    target: { value: "Classroom school" },
  });
  fireEvent.change(screen.getByLabelText("Lesson plan title"), {
    target: { value: "Equal shares" },
  });
  fireEvent.click(screen.getByRole("button", { name: "2. Lesson detail" }));
  for (const label of [
    "Critical thinking",
    "Collaboration",
    "Creativity",
    "Innovation",
    "Communication",
    "Natural",
    "Artificial",
    "Technological",
  ])
    fireEvent.click(screen.getByLabelText(label));
  fireEvent.change(screen.getByLabelText("Sub-topic"), {
    target: { value: "Halves" },
  });
  fireEvent.change(screen.getByLabelText("Lesson goal"), {
    target: { value: "Recognise equal parts" },
  });
  fireEvent.change(screen.getByLabelText("References"), {
    target: { value: "Original classroom handout" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "3. Lesson progression" }),
  );
  for (const label of [
    "Introduction",
    "Lesson development",
    "Exercise/assessment",
    "Homework",
    "Conclusion",
    "Reinforcement",
    "Extension",
  ])
    expect(screen.getByRole("group", { name: label })).toBeVisible();
  fireEvent.change(screen.getByLabelText("Introduction: Learner activities"), {
    target: { value: "Fold a circle" },
  });
  fireEvent.change(screen.getByLabelText("Introduction: Teacher activities"), {
    target: { value: "Model equal sharing" },
  });
  fireEvent.change(screen.getByLabelText("Introduction: minutes"), {
    target: { value: "5" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "4. Lesson evaluation and review" }),
  );
  fireEvent.change(screen.getByLabelText("Lesson evaluation"), {
    target: { value: "Review after teaching" },
  });
  const draft = JSON.parse(
    screen.getByTestId("saved-draft").textContent!,
  ) as LessonDocument;
  expect(draft.setup.title).toBe("Equal shares");
  expect(draft.setup.objectives).toBe("Recognise equal parts");
  expect(draft.ministryForm?.generalCompetences).toHaveLength(5);
  expect(draft.ministryForm?.learningEnvironments).toEqual([
    "natural",
    "artificial",
    "technological",
  ]);
  expect(draft.ministryForm?.subTopic).toBe("Halves");
  expect(draft.ministryForm?.progression).toHaveLength(7);
  expect(draft.sections.find((s) => s.code === "starter")).toMatchObject({
    body: "Model equal sharing",
    minutes: 5,
  });
  expect(draft.ministryForm?.evaluation).toBe("Review after teaching");
});
it("recent navigation stores only bounded IDs and is isolated by account and organisation", () => {
  sessionStorage.clear();
  for (let i = 0; i < 10; i++)
    rememberLesson("school-a", "teacher-a", `lesson-${i}`);
  expect(recentLessonIds("school-a", "teacher-a")).toEqual([
    "lesson-9",
    "lesson-8",
    "lesson-7",
    "lesson-6",
    "lesson-5",
    "lesson-4",
  ]);
  expect(recentLessonIds("school-b", "teacher-a")).toEqual([]);
  expect(recentLessonIds("school-a", "teacher-b")).toEqual([]);
  rememberLesson("school-a", "teacher-a", "lesson-6");
  expect(recentLessonIds("school-a", "teacher-a")[0]).toBe("lesson-6");
  sessionStorage.clear();
});
