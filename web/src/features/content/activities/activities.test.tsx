import { fireEvent, render, screen, act } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { webcrypto } from "node:crypto";
import { ActivityPlayer } from "./ActivityPlayer";
import { sampleActivity } from "./ActivityComposer";
import { offlineActivityHtml } from "./offlineActivity";
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});
describe("safe interactive teaching content", () => {
  it.each([
    "matching",
    "sequencing",
    "fill-blank",
    "number-line",
    "fraction",
    "place-value-exercise",
  ] as const)("plays and marks %s with accessible controls", (kind) => {
    const definition = {
      ...sampleActivity(),
      schemaVersion: 2,
      kind,
      number: 2,
      minimum: 0,
      maximum: 10,
      denominator: 4,
      questions: ["matching", "sequencing", "fill-blank"].includes(kind)
        ? [
            {
              prompt: "Complete",
              options: ["one", "two"],
              correctOption: 0,
              explanation: "Count carefully",
            },
          ]
        : [],
    };
    render(<ActivityPlayer definition={definition} />);
    if (kind === "matching")
      fireEvent.change(screen.getByLabelText("Matching answer"), {
        target: { value: "0" },
      });
    if (kind === "sequencing") {
      fireEvent.change(screen.getByLabelText("Position 1"), {
        target: { value: "0" },
      });
      fireEvent.change(screen.getByLabelText("Position 2"), {
        target: { value: "1" },
      });
    }
    if (kind === "fill-blank")
      fireEvent.change(screen.getByLabelText("Your answer"), {
        target: { value: " ONE " },
      });
    if (kind === "number-line")
      fireEvent.input(screen.getByRole("slider"), { target: { value: "2" } });
    if (kind === "fraction") {
      fireEvent.click(screen.getByRole("button", { name: "Part 1" }));
      fireEvent.click(screen.getByRole("button", { name: "Part 2" }));
    }
    if (kind === "place-value-exercise") {
      fireEvent.click(screen.getByRole("button", { name: "Add one ones" }));
      fireEvent.click(screen.getByRole("button", { name: "Add one ones" }));
    }
    fireEvent.click(screen.getByRole("button", { name: "Check answers" }));
    expect(
      screen.getByRole("status", { name: "Practice result" }),
    ).toHaveTextContent("1 of 1 correct");
    fireEvent.click(
      screen.getByRole("button", { name: "Try the activity again" }),
    );
    expect(
      screen.getByRole("status", { name: "Practice result" }),
    ).toBeEmptyDOMElement();
  });
  it("marks real selected answers with explanations and supports another attempt", () => {
    render(<ActivityPlayer definition={sampleActivity()} />);
    fireEvent.click(
      screen.getByRole("radio", { name: "One of three equal parts" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Check answers" }));
    expect(screen.getByRole("status")).toHaveTextContent("0 of 1 correct");
    expect(screen.getByText(/Try again. One half/)).toBeVisible();
    fireEvent.click(
      screen.getByRole("radio", { name: "One of two equal parts" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Check answers" }));
    expect(screen.getByRole("status")).toHaveTextContent("1 of 1 correct");
    fireEvent.click(
      screen.getByRole("button", { name: "Try the activity again" }),
    );
    expect(
      screen.getByRole("radio", { name: "One of two equal parts" }),
    ).not.toBeChecked();
  });
  it("steps a correct place-value explanation without autoplay and supports play, pause and restart", () => {
    vi.useFakeTimers();
    render(
      <ActivityPlayer
        definition={{
          ...sampleActivity(),
          kind: "place-value",
          questions: [],
          number: 2436,
        }}
      />,
    );
    expect(screen.getByRole("status")).toHaveTextContent(
      "2 thousands have a value of 2000",
    );
    act(() => vi.advanceTimersByTime(10000));
    expect(screen.getByRole("status")).toHaveTextContent("Step 1");
    fireEvent.click(screen.getByRole("button", { name: "Next place" }));
    expect(screen.getByRole("status")).toHaveTextContent(
      "4 hundreds have a value of 400",
    );
    fireEvent.click(screen.getByRole("button", { name: "Play explanation" }));
    act(() => vi.advanceTimersByTime(2000));
    expect(screen.getByRole("status")).toHaveTextContent(
      "3 tens have a value of 30",
    );
    fireEvent.click(screen.getByRole("button", { name: "Pause explanation" }));
    act(() => vi.advanceTimersByTime(4000));
    expect(screen.getByRole("status")).toHaveTextContent("Step 3");
    fireEvent.click(
      screen.getByRole("button", { name: "Restart explanation" }),
    );
    expect(screen.getByRole("status")).toHaveTextContent("Step 1");
  });
  it("renders authored HTML as text and exports a hash-restricted offline runtime", async () => {
    const definition = {
      ...sampleActivity(),
      title: '<img src=x onerror="alert(1)">',
    };
    const { container } = render(<ActivityPlayer definition={definition} />);
    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByRole("heading")).toHaveTextContent("<img");
    vi.stubGlobal("crypto", webcrypto);
    const html = await offlineActivityHtml(definition, "approved-version");
    expect(html).not.toContain("<img src=x");
    expect(html).toContain("\\u003cimg");
    expect(html).toContain("script-src 'sha256-");
    expect(html).toContain("default-src 'none'");
    expect(html).not.toContain("localStorage");
    expect(html).not.toContain("fetch(");
  });
});
