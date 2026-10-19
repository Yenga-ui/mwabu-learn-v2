export interface ActivityQuestion {
  prompt: string;
  options: string[];
  correctOption: number;
  explanation: string;
}
export interface ActivityDefinition {
  schemaVersion: number;
  kind:
    | "quiz"
    | "place-value"
    | "matching"
    | "sequencing"
    | "fill-blank"
    | "number-line"
    | "fraction"
    | "place-value-exercise";
  title: string;
  instructions: string;
  language: string;
  questions: ActivityQuestion[];
  number: number;
  minimum?: number;
  maximum?: number;
  denominator?: number;
}
export const activityMime = "application/vnd.mwabu.activity+json";

// Deliberately self-contained: the same fixed renderer runs online and in an offline file.
// All authored values are text/numbers. No HTML, script, URLs or dynamic code is evaluated.
export function mountActivity(
  host: HTMLElement,
  activity: ActivityDefinition,
  wholeClass = false,
) {
  host.replaceChildren();
  const textValid = (value: unknown, maximum: number) =>
    typeof value === "string" &&
    value.trim().length > 0 &&
    value.length <= maximum;
  const questionsKind = [
    "quiz",
    "matching",
    "sequencing",
    "fill-blank",
  ].includes(activity?.kind);
  const minimum = activity?.minimum ?? 0,
    maximum = activity?.maximum ?? 20,
    denominator = activity?.denominator ?? 4;
  if (
    !activity ||
    ![1, 2].includes(activity.schemaVersion) ||
    ![
      "quiz",
      "place-value",
      "matching",
      "sequencing",
      "fill-blank",
      "number-line",
      "fraction",
      "place-value-exercise",
    ].includes(activity.kind) ||
    (activity.schemaVersion === 1 &&
      !["quiz", "place-value"].includes(activity.kind)) ||
    !textValid(activity.title, 200) ||
    !textValid(activity.instructions, 2000) ||
    !textValid(activity.language, 35) ||
    !Number.isInteger(activity.number) ||
    activity.number < 0 ||
    activity.number > 9999 ||
    !Array.isArray(activity.questions) ||
    (questionsKind
      ? activity.questions.length < 1 ||
        activity.questions.length > 10 ||
        activity.questions.some(
          (q) =>
            !q ||
            !textValid(q.prompt, 1000) ||
            !textValid(q.explanation, 2000) ||
            !Array.isArray(q.options) ||
            q.options.length < (activity.kind === "fill-blank" ? 1 : 2) ||
            q.options.length > 6 ||
            q.options.some((x) => !textValid(x, 300)) ||
            new Set(q.options.map((x) => x.trim().toUpperCase())).size !==
              q.options.length ||
            !Number.isInteger(q.correctOption) ||
            q.correctOption < 0 ||
            q.correctOption >= q.options.length,
        )
      : activity.questions.length !== 0) ||
    (activity.kind === "fraction" &&
      (!Number.isInteger(denominator) ||
        denominator < 2 ||
        denominator > 24 ||
        activity.number > denominator)) ||
    (activity.kind === "number-line" &&
      (!Number.isInteger(minimum) ||
        !Number.isInteger(maximum) ||
        minimum < -1000 ||
        maximum > 1000 ||
        maximum <= minimum ||
        maximum - minimum > 100 ||
        activity.number < minimum ||
        activity.number > maximum))
  ) {
    host.textContent =
      "This activity is invalid or unsupported. Check its definition before saving or playing it.";
    return () => host.replaceChildren();
  }
  const prefix = "practice-" + Math.random().toString(36).slice(2);
  let timer: ReturnType<typeof setInterval> | undefined;
  function element<K extends keyof HTMLElementTagNameMap>(
    tag: K,
    text = "",
    parent: HTMLElement = host,
  ) {
    const node = document.createElement(tag);
    node.textContent = text;
    parent.append(node);
    return node;
  }
  function button(
    label: string,
    action: () => void,
    parent: HTMLElement = host,
  ) {
    const node = element("button", label, parent);
    node.type = "button";
    node.className = "button secondary";
    node.onclick = action;
    return node;
  }
  host.lang = activity.language;
  element("h2", activity.title);
  element("p", activity.instructions);
  if (activity.kind !== "quiz" && activity.kind !== "place-value") {
    const form = element("form");
    const feedback: HTMLElement[] = [];
    const read: (() => boolean)[] = [];
    const resets: (() => void)[] = [];
    if (["matching", "sequencing", "fill-blank"].includes(activity.kind)) {
      activity.questions.forEach((q, index) => {
        const group = element("fieldset", "", form);
        element("legend", `${index + 1}. ${q.prompt}`, group);
        if (activity.kind === "fill-blank") {
          const label = element("label", "Your answer", group);
          const input = element("input", "", label);
          input.required = true;
          input.maxLength = 300;
          read.push(() =>
            q.options.some(
              (x) =>
                x.trim().normalize().toUpperCase() ===
                input.value.trim().normalize().toUpperCase(),
            ),
          );
        } else {
          const choices: HTMLSelectElement[] = [];
          const count = activity.kind === "sequencing" ? q.options.length : 1;
          for (let position = 0; position < count; position++) {
            const label = element(
              "label",
              count === 1 ? "Matching answer" : `Position ${position + 1}`,
              group,
            );
            const select = element("select", "", label);
            select.required = true;
            const empty = element("option", "Choose…", select);
            empty.value = "";
            [...q.options.keys()].reverse().forEach((i) => {
              const option = element("option", q.options[i], select);
              option.value = String(i);
            });
            choices.push(select);
          }
          read.push(() =>
            choices.every(
              (select, i) =>
                select.value ===
                String(activity.kind === "sequencing" ? i : q.correctOption),
            ),
          );
        }
        feedback.push(element("p", "", group));
      });
    } else {
      const group = element("fieldset", "", form);
      element(
        "legend",
        activity.kind === "fraction"
          ? `Shade ${activity.number} of ${activity.denominator ?? 4} equal parts`
          : `Build ${activity.number}`,
        group,
      );
      if (activity.kind === "number-line") {
        const minimum = activity.minimum ?? 0,
          maximum = activity.maximum ?? 20;
        const label = element(
          "label",
          `Number line from ${minimum} to ${maximum}`,
          group,
        );
        const input = element("input", "", label);
        input.type = "range";
        input.min = String(minimum);
        input.max = String(maximum);
        input.step = "1";
        input.value = String(minimum);
        const output = element("output", input.value, group);
        input.oninput = () => {
          output.textContent = input.value;
        };
        element(
          "p",
          "Use the arrow keys to move one step, or select a position on the line.",
          group,
        );
        read.push(() => Number(input.value) === activity.number);
        resets.push(() => {
          input.value = String(minimum);
          output.textContent = input.value;
        });
      } else if (activity.kind === "fraction") {
        const selected = new Set<number>();
        const parts: HTMLButtonElement[] = [];
        const caption = element(
          "p",
          `0 / ${activity.denominator ?? 4} shaded`,
          group,
        );
        const row = element("div", "", group);
        row.className = "fraction-parts";
        for (let i = 0; i < (activity.denominator ?? 4); i++) {
          const part = button(
            `Part ${i + 1}`,
            () => {
              if (selected.has(i)) selected.delete(i);
              else selected.add(i);
              part.setAttribute("aria-pressed", String(selected.has(i)));
              caption.textContent = `${selected.size} / ${activity.denominator ?? 4} shaded`;
            },
            row,
          );
          part.setAttribute("aria-pressed", "false");
          parts.push(part);
        }
        read.push(() => selected.size === activity.number);
        resets.push(() => {
          selected.clear();
          parts.forEach((x) => x.setAttribute("aria-pressed", "false"));
          caption.textContent = `0 / ${activity.denominator ?? 4} shaded`;
        });
      } else {
        const digits = [0, 0, 0, 0];
        const inputs: HTMLInputElement[] = [];
        const output = element("output", "0", group);
        const update = () => {
          output.textContent = `${digits.reduce((sum, x, i) => sum + x * 10 ** (3 - i), 0)} = ${digits.map((x, i) => x * 10 ** (3 - i)).join(" + ")}`;
        };
        ["Thousands", "Hundreds", "Tens", "Ones"].forEach((name, i) => {
          const row = element("div", "", group);
          const label = element("label", name, row);
          const input = element("input", "", label);
          input.type = "number";
          input.min = "0";
          input.max = "9";
          input.value = "0";
          input.required = true;
          inputs.push(input);
          input.oninput = () => {
            digits[i] = Number(input.value);
            update();
          };
          const set = (delta: number) => {
            digits[i] = Math.max(0, Math.min(9, digits[i] + delta));
            input.value = String(digits[i]);
            update();
          };
          button(`Add one ${name.toLowerCase()}`, () => set(1), row);
          button(`Remove one ${name.toLowerCase()}`, () => set(-1), row);
        });
        read.push(
          () => digits.join("") === String(activity.number).padStart(4, "0"),
        );
        resets.push(() => {
          digits.fill(0);
          inputs.forEach((x) => (x.value = "0"));
          update();
        });
        update();
      }
      feedback.push(element("p", "", group));
    }
    const submit = element("button", "Check answers", form);
    submit.hidden = wholeClass;
    submit.type = "submit";
    submit.className = "button";
    const summary = element("p", "", form);
    summary.setAttribute("role", "status");
    summary.setAttribute("aria-label", "Practice result");
    form.onsubmit = (event) => {
      event.preventDefault();
      let score = 0;
      read.forEach((mark, i) => {
        const correct = mark();
        if (correct) score++;
        feedback[i].textContent =
          `${correct ? "Correct." : "Try again."} ${activity.questions[i]?.explanation ?? "Use the equal parts or place values to build the target, then check again."}`;
      });
      summary.textContent = `${score} of ${read.length} correct. This is practice feedback; no learner results are uploaded.`;
    };
    button(
      "Try the activity again",
      () => {
        form.reset();
        resets.forEach((reset) => reset());
        summary.textContent = "";
        feedback.forEach((x) => (x.textContent = ""));
      },
      form,
    );
  } else if (activity.kind === "quiz") {
    const form = element("form");
    const answers: HTMLInputElement[][] = [];
    const feedback: HTMLElement[] = [];
    activity.questions.forEach((question, index) => {
      const group = element("fieldset", "", form);
      element("legend", `${index + 1}. ${question.prompt}`, group);
      answers[index] = question.options.map((option, choice) => {
        const label = element("label", "", group);
        label.className = "check-label";
        const input = element("input", "", label);
        input.type = "radio";
        input.name = `${prefix}-${index}`;
        input.value = String(choice);
        input.required = true;
        label.append(document.createTextNode(option));
        return input;
      });
      feedback[index] = element("p", "", group);
    });
    const check = element("button", "Check answers", form);
    check.hidden = wholeClass;
    check.type = "submit";
    check.className = "button";
    const summary = element("p", "", form);
    summary.setAttribute("role", "status");
    form.onsubmit = (event) => {
      event.preventDefault();
      let score = 0;
      activity.questions.forEach((question, index) => {
        const chosen = answers[index].findIndex((x) => x.checked);
        const correct = chosen === question.correctOption;
        if (correct) score++;
        feedback[index].textContent =
          `${correct ? "Correct." : "Try again."} ${question.explanation}`;
      });
      summary.textContent = `${score} of ${activity.questions.length} correct. This is practice feedback; no learner results are uploaded.`;
    };
    button(
      "Try the activity again",
      () => {
        form.reset();
        summary.textContent = "";
        feedback.forEach((x) => (x.textContent = ""));
        answers[0][0].focus();
      },
      form,
    );
  } else {
    const names = ["thousands", "hundreds", "tens", "ones"];
    const units = [1000, 100, 10, 1];
    const digits = String(activity.number)
      .padStart(4, "0")
      .split("")
      .map(Number);
    let step = 0;
    let playing = false;
    let speed = 1;
    const scene = element("div");
    scene.className = "place-value-scene";
    const caption = element("p");
    caption.setAttribute("role", "status");
    const controls = element("div");
    controls.className = "actions";
    function stop() {
      if (timer !== undefined) clearInterval(timer);
      timer = undefined;
      playing = false;
      play.textContent = "Play explanation";
    }
    function render() {
      scene.replaceChildren();
      const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("viewBox", "0 0 480 100");
      svg.setAttribute("role", "img");
      svg.setAttribute(
        "aria-label",
        `${digits[step]} ${names[step]}, worth ${digits[step] * units[step]}`,
      );
      for (let i = 0; i < digits[step]; i++) {
        const rect = document.createElementNS(svg.namespaceURI, "rect");
        rect.setAttribute("x", String(5 + i * 52));
        rect.setAttribute("y", "5");
        rect.setAttribute("width", "44");
        rect.setAttribute("height", "70");
        rect.setAttribute("fill", "#250e62");
        svg.append(rect);
        const text = document.createElementNS(svg.namespaceURI, "text");
        text.setAttribute("x", String(27 + i * 52));
        text.setAttribute("y", "45");
        text.setAttribute("text-anchor", "middle");
        text.setAttribute("fill", "white");
        text.setAttribute("font-size", "12");
        text.textContent = String(units[step]);
        svg.append(text);
      }
      scene.append(svg);
      element(
        "p",
        `${activity.number} = ${digits.map((digit, i) => digit * units[i]).join(" + ")}`,
        scene,
      );
      caption.textContent = `Step ${step + 1} of 4: ${digits[step]} ${names[step]} have a value of ${digits[step] * units[step]}.`;
      previous.disabled = step === 0;
      next.disabled = step === 3;
    }
    const previous = button(
      "Previous place",
      () => {
        stop();
        step = Math.max(0, step - 1);
        render();
      },
      controls,
    );
    const next = button(
      "Next place",
      () => {
        stop();
        step = Math.min(3, step + 1);
        render();
      },
      controls,
    );
    function start() {
      if (step === 3) step = 0;
      playing = true;
      play.textContent = "Pause explanation";
      render();
      timer = setInterval(() => {
        step = Math.min(3, step + 1);
        render();
        if (step === 3) stop();
      }, 2000 / speed);
    }
    const play = button(
      "Play explanation",
      () => {
        if (playing) stop();
        else start();
      },
      controls,
    );
    button(
      "Restart explanation",
      () => {
        stop();
        step = 0;
        render();
      },
      controls,
    );
    const label = element("label", "Playback speed", controls);
    const select = element("select", "", label);
    [0.5, 1, 2].forEach((value) => {
      const option = element("option", `${value}×`, select);
      option.value = String(value);
      option.selected = value === 1;
    });
    select.onchange = () => {
      const resume = playing;
      stop();
      speed = Number(select.value);
      if (resume) start();
    };
    element(
      "p",
      "Use Next to work at your own pace. The explanation never starts automatically.",
    );
    render();
  }
  return () => {
    if (timer !== undefined) clearInterval(timer);
    host.replaceChildren();
  };
}
