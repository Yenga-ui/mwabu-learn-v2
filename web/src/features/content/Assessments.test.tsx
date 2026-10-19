import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { it, expect, vi } from "vitest";
import { AssessmentForm } from "./Assessments";
import { api } from "../../api/client";
vi.mock("../../api/client", async () => ({
  ...(await vi.importActual<typeof import("../../api/client")>(
    "../../api/client",
  )),
  api: vi.fn(),
}));
it("retries an unchanged online submission with the same key and displays server marking without persisting answers", async () => {
  const onSaved = vi.fn();
  const storage = vi.spyOn(Storage.prototype, "setItem");
  vi.mocked(api)
    .mockRejectedValueOnce(new Error("Connection interrupted"))
    .mockResolvedValueOnce({
      id: "attempt",
      score: 0,
      total: 1,
      correct: [false],
    });
  render(
    <AssessmentForm
      base="/api/organisations/school/assessments"
      onSaved={onSaved}
      data={{
        id: "bank",
        assetId: "version",
        title: "Halves",
        kind: "quiz",
        instructions: "Choose",
        number: 0,
        minimum: 0,
        maximum: 20,
        denominator: 4,
        questions: [
          {
            prompt: "One half",
            options: [
              { value: "0", text: "Two equal parts" },
              { value: "1", text: "Three parts" },
            ],
          },
        ],
      }}
    />,
  );
  fireEvent.change(screen.getByLabelText("Your answer"), {
    target: { value: "1" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Submit assessment" }));
  await screen.findByText("Connection interrupted");
  await waitFor(() =>
    expect(
      screen.getByRole("button", { name: "Submit assessment" }),
    ).toBeEnabled(),
  );
  fireEvent.click(screen.getByRole("button", { name: "Submit assessment" }));
  await screen.findByText("Server marked: 0 of 1 correct.");
  const first = vi.mocked(api).mock.calls[0][1]?.body as {
    submissionKey: string;
    answers: string[][];
  };
  const second = vi.mocked(api).mock.calls[1][1]?.body as typeof first;
  expect(first.submissionKey).toBe(second.submissionKey);
  expect(second.answers).toEqual([["1"]]);
  expect(second).not.toHaveProperty("score");
  expect(onSaved).toHaveBeenCalledOnce();
  expect(storage).not.toHaveBeenCalled();
  storage.mockRestore();
});
