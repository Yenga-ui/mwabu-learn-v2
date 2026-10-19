import type { ActivityDefinition, mountActivity } from "./activityRuntime";

// Self-contained and shared by React and integrity-checked offline packages.
export function mountWholeClass(
  host: HTMLElement,
  definition: ActivityDefinition,
  mount: typeof mountActivity,
) {
  let index = 0;
  let partitions = definition.denominator ?? 4;
  let cleanup: (() => void) | undefined;
  function render() {
    cleanup?.();
    host.replaceChildren();
    const text = (tag: string, value: string) => {
      const node = document.createElement(tag);
      node.textContent = value;
      host.append(node);
      return node;
    };
    const button = (label: string, action: () => void, disabled = false) => {
      const node = text("button", label) as HTMLButtonElement;
      node.type = "button";
      node.className = "button secondary";
      node.disabled = disabled;
      node.onclick = action;
      return node;
    };
    text("p", "ASK · DISCUSS · REVEAL · EXPLAIN");
    text(
      "p",
      "This is a classroom demonstration. No learner scores are recorded.",
    );
    const displayed =
      definition.kind === "fraction"
        ? {
            ...definition,
            denominator: partitions,
            instructions: `Explore ${definition.number} of ${partitions} equal parts together.`,
          }
        : definition;
    const question = displayed.questions[index];
    const demo = text("div", "");
    demo.className = "activity-player";
    cleanup = mount(
      demo,
      question ? { ...displayed, questions: [question] } : displayed,
      true,
    );
    demo.querySelectorAll("form").forEach((form) => {
      form.onsubmit = (event) => event.preventDefault();
    });
    const solution = question
      ? definition.kind === "sequencing"
        ? question.options.join(" → ")
        : definition.kind === "fill-blank"
          ? question.options.join(" / ")
          : question.options[question.correctOption]
      : definition.kind === "fraction"
        ? `${definition.number} / ${partitions}`
        : String(definition.number);
    const answer = text("p", "");
    answer.setAttribute("role", "status");
    const explanation = text("p", "");
    let revealed = false;
    let explained = false;
    const reveal = button("Reveal activity answer", () => {
      revealed = !revealed;
      answer.textContent = revealed ? `Answer: ${solution}` : "";
      explanation.textContent = "";
      explained = false;
      explain.disabled = !revealed;
      explain.textContent = "Show explanation";
      reveal.textContent = revealed
        ? "Hide activity answer"
        : "Reveal activity answer";
    });
    const explain = button(
      "Show explanation",
      () => {
        explained = !explained;
        explanation.textContent = explained
          ? (question?.explanation ??
            "Discuss how the equal parts, number line or place values demonstrate the answer.")
          : "";
        explain.textContent = explained
          ? "Hide explanation"
          : "Show explanation";
      },
      true,
    );
    if (definition.kind === "fraction") {
      const label = text("label", "Equal partitions");
      const select = document.createElement("select");
      for (let n = Math.max(2, definition.number); n <= 24; n++) {
        const option = document.createElement("option");
        option.value = String(n);
        option.textContent = String(n);
        select.append(option);
      }
      select.value = String(partitions);
      select.onchange = () => {
        partitions = Number(select.value);
        render();
      };
      label.append(select);
    }
    button("Reset demonstration", () => {
      partitions = definition.denominator ?? 4;
      render();
    });
    if (definition.questions.length > 1) {
      button(
        "Previous question",
        () => {
          index--;
          render();
        },
        index === 0,
      );
      button(
        "Next question",
        () => {
          index++;
          render();
        },
        index === definition.questions.length - 1,
      );
      text("p", `Question ${index + 1} of ${definition.questions.length}`);
    }
  }
  render();
  return () => {
    cleanup?.();
    host.replaceChildren();
  };
}
