import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { WholeClassActivity } from "./WholeClassActivity";
import type { ActivityDefinition } from "./activityRuntime";
describe("whole-class activity", () => {
  it("adjusts equal partitions for a demonstration without mutating the published definition", () => {
    const definition: ActivityDefinition = {
      schemaVersion: 2,
      kind: "fraction",
      title: "Equal parts",
      instructions: "Explore halves",
      language: "en",
      number: 2,
      denominator: 4,
      questions: [],
    };
    render(<WholeClassActivity definition={definition} />);
    fireEvent.change(screen.getByLabelText("Equal partitions"), {
      target: { value: "8" },
    });
    expect(screen.getByRole("button", { name: "Part 8" })).toBeVisible();
    fireEvent.click(
      screen.getByRole("button", { name: "Reveal activity answer" }),
    );
    expect(screen.getByText("Answer: 2 / 8")).toBeVisible();
    expect(definition.denominator).toBe(4);
    fireEvent.click(
      screen.getByRole("button", { name: "Reset demonstration" }),
    );
    expect(screen.queryByRole("button", { name: "Part 8" })).toBeNull();
    expect(screen.queryByText("Answer: 2 / 8")).toBeNull();
  });
  it("hides answers and explanations until explicit reveal, resets, and never displays scores", () => {
    const definition: ActivityDefinition = {
      schemaVersion: 1,
      kind: "quiz",
      title: "Equal shares",
      instructions: "Discuss together",
      language: "en",
      number: 0,
      questions: [
        {
          prompt: "Which fraction is one half?",
          options: ["1/2", "1/3"],
          correctOption: 0,
          explanation: "Two equal parts make halves.",
        },
      ],
    };
    render(<WholeClassActivity definition={definition} />);
    expect(screen.queryByText("Answer: 1/2")).toBeNull();
    expect(screen.queryByText("Two equal parts make halves.")).toBeNull();
    expect(screen.queryByRole("button", { name: "Check answers" })).toBeNull();
    fireEvent.click(
      screen.getByRole("button", { name: "Reveal activity answer" }),
    );
    expect(screen.getByText("Answer: 1/2")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "Show explanation" }));
    expect(screen.getByText("Two equal parts make halves.")).toBeVisible();
    fireEvent.click(
      screen.getByRole("button", { name: "Reset demonstration" }),
    );
    expect(screen.queryByText("Answer: 1/2")).toBeNull();
    expect(screen.queryByText("Two equal parts make halves.")).toBeNull();
    expect(screen.queryByText(/of .* correct/)).toBeNull();
  });
});
