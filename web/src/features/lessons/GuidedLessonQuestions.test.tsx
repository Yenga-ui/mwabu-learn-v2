import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, it } from "vitest";
import GuidedLessonQuestions from "./GuidedLessonQuestions";
import { blankSetup } from "./types";

it("retains classroom answers through review and back navigation without requesting generation", () => {
  function Harness() {
    const [setup, change] = useState({
      ...blankSetup(),
      title: "Fractions",
      subject: "Mathematics",
      targetLearners: "Grade 4",
    });
    return (
      <>
        <GuidedLessonQuestions value={setup} change={change} />
        <output aria-label="Saved answers">{JSON.stringify(setup)}</output>
      </>
    );
  }
  render(<Harness />);
  fireEvent.change(screen.getByLabelText("How long is your lesson?"), {
    target: { value: "60" },
  });
  fireEvent.click(screen.getByLabelText("Paper and pencils"));
  fireEvent.click(
    screen.getByRole("button", { name: "Continue to learning and support" }),
  );
  fireEvent.change(
    screen.getByLabelText("What should learners be able to do by the end?"),
    { target: { value: "Compare halves and quarters" } },
  );
  fireEvent.click(
    screen.getByRole("button", { name: "Continue to review your answers" }),
  );
  expect(screen.getByText("Compare halves and quarters")).toBeInTheDocument();
  expect(screen.getByText("Paper and pencils")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "1. Your class" }));
  expect(screen.getByLabelText("How long is your lesson?")).toHaveValue("60");
  expect(screen.getByLabelText("Paper and pencils")).toBeChecked();
  expect(
    JSON.parse(screen.getByLabelText("Saved answers").textContent!).objectives,
  ).toBe("Compare halves and quarters");
});
