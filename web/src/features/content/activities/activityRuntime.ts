export interface ActivityQuestion {
  prompt: string;
  options: string[];
  correctOption: number;
  explanation: string;
}
export interface ActivityDefinition {
  schemaVersion: number;
  kind: "quiz" | "place-value";
  title: string;
  instructions: string;
  language: string;
  questions: ActivityQuestion[];
  number: number;
}
export const activityMime = "application/vnd.mwabu.activity+json";

// Deliberately self-contained: the same fixed renderer runs online and in an offline file.
// All authored values are text/numbers. No HTML, script, URLs or dynamic code is evaluated.
export function mountActivity(host: HTMLElement, activity: ActivityDefinition) {
  host.replaceChildren();
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
  if (activity.kind === "quiz") {
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
