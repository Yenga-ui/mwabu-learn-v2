import { useState } from "react";
import type { Setup } from "./types";
import { Users, Lightbulb, ClipboardCheck } from "lucide-react";

/** Answers are saved in the lesson, so the durable AI job uses the same revision. */
export default function GuidedLessonQuestions({
  value,
  change,
}: {
  value: Setup;
  change: (value: Setup) => void;
}) {
  const [step, setStep] = useState(0);
  const headings = [
    "Your class",
    "Learning and support",
    "Review your answers",
  ];
  const questionIcons = [Users, Lightbulb, ClipboardCheck];
  const set = <K extends keyof Setup>(key: K, answer: Setup[K]) =>
    change({ ...value, [key]: answer });
  return (
    <section className="guided-planning" aria-label="Guided lesson questions">
      <p>
        Answer a few questions. AI will draft the complete lesson for you to
        check.
      </p>
      <nav aria-label="Planning questions" className="teaching-choice-grid">
        {headings.map((heading, index) => {
          const Icon = questionIcons[index];
          return (
            <button
              type="button"
              key={heading}
              className="teaching-choice"
              aria-current={step === index ? "step" : undefined}
              onClick={() => setStep(index)}
            >
              <Icon size={24} aria-hidden="true" />
              {index + 1}. {heading}
            </button>
          );
        })}
      </nav>
      <h3 tabIndex={-1}>{headings[step]}</h3>
      {step === 0 && (
        <div className="lesson-grid">
          <p>
            {value.targetLearners} · {value.subject} · {value.title}. You can
            change the topic in Lesson setup.
          </p>
          <label>
            How long is your lesson?
            <select
              value={value.durationMinutes}
              onChange={(e) => set("durationMinutes", Number(e.target.value))}
            >
              {[...new Set([30, 35, 40, 45, 60, 80, value.durationMinutes])]
                .sort((a, b) => a - b)
                .map((minutes) => (
                  <option key={minutes} value={minutes}>
                    {minutes} minutes
                  </option>
                ))}
            </select>
          </label>
          <label>
            How many learners?
            <input
              type="number"
              min={1}
              max={1000}
              value={value.classSize}
              onChange={(e) => set("classSize", Number(e.target.value))}
            />
          </label>
          <label>
            Which language will you teach in?
            <select
              value={value.language}
              onChange={(e) => set("language", e.target.value)}
            >
              {[
                ...new Set([
                  "en",
                  "bem",
                  "nya",
                  "toi",
                  "loz",
                  "lun",
                  "lue",
                  "kqn",
                  value.language,
                ]),
              ].map((language) => (
                <option key={language} value={language}>
                  {(
                    {
                      en: "English",
                      bem: "Bemba",
                      nya: "Nyanja",
                      toi: "Tonga",
                      loz: "Lozi",
                      lun: "Lunda",
                      lue: "Luvale",
                      kqn: "Kaonde",
                    } as Record<string, string>
                  )[language] ?? language}
                </option>
              ))}
            </select>
          </label>
          <fieldset>
            <legend>
              Choose classroom materials, or describe your own below
            </legend>
            {[
              "Chalkboard and chalk",
              "Paper and pencils",
              "Locally available objects",
              "Shared screen and published activities",
            ].map((material) => (
              <label className="checkbox" key={material}>
                <input
                  type="checkbox"
                  checked={value.availableResources
                    .split("; ")
                    .includes(material)}
                  onChange={(e) =>
                    set(
                      "availableResources",
                      e.target.checked
                        ? [value.availableResources, material]
                            .filter(Boolean)
                            .join("; ")
                        : value.availableResources
                            .split("; ")
                            .filter((item) => item !== material)
                            .join("; "),
                    )
                  }
                />
                {material}
              </label>
            ))}
          </fieldset>
          <label>
            What will you have available?
            <textarea
              maxLength={2000}
              value={value.availableResources}
              onChange={(e) => set("availableResources", e.target.value)}
              placeholder="For example: chalkboard, paper and bottle tops. Leave blank if unsure."
            />
          </label>
        </div>
      )}
      {step === 1 && (
        <div className="lesson-grid">
          <label>
            What should learners be able to do by the end?
            <textarea
              maxLength={2000}
              value={value.objectives}
              onChange={(e) => set("objectives", e.target.value)}
              placeholder="Optional. AI can suggest objectives from your selected topic and authorised sources."
            />
          </label>
          <label>
            What do learners already know? What is your classroom like?
            <textarea
              maxLength={2000}
              value={value.teachingContext}
              onChange={(e) => set("teachingContext", e.target.value)}
              placeholder="For example: learners can recognise halves; we share one screen."
            />
          </label>
          <label>
            What support will help your class?
            <textarea
              maxLength={2000}
              value={value.supportRequirements}
              onChange={(e) => set("supportRequirements", e.target.value)}
              placeholder="For example: large print, simpler language or extra challenge. Do not include learner names."
            />
          </label>
        </div>
      )}
      {step === 2 && (
        <>
          <dl className="lesson-grid">
            <div>
              <dt>Lesson</dt>
              <dd>
                {value.title} · {value.targetLearners} · {value.subject}
              </dd>
            </div>
            <div>
              <dt>Class</dt>
              <dd>
                {value.durationMinutes} minutes · {value.classSize} learners ·{" "}
                {value.language}
              </dd>
            </div>
            <div>
              <dt>Learning goal</dt>
              <dd>
                {value.objectives ||
                  "Suggest from the selected topic and available evidence"}
              </dd>
            </div>
            <div>
              <dt>Resources</dt>
              <dd>
                {value.availableResources ||
                  "Not specified; suggest accessible classroom materials"}
              </dd>
            </div>
            <div>
              <dt>Support</dt>
              <dd>{value.supportRequirements || "Not specified"}</dd>
            </div>
          </dl>
          <p>
            Your draft will include objectives, timed teaching steps, teacher
            guidance, learner explanations, questions and a recap. Check the
            result before using it. AI does not publish your lesson.
          </p>
        </>
      )}
      <div className="actions">
        {step > 0 && (
          <button
            type="button"
            className="button secondary"
            onClick={() => setStep(step - 1)}
          >
            Back
          </button>
        )}
        {step < 2 && (
          <button
            type="button"
            className="button"
            onClick={() => setStep(step + 1)}
          >
            Continue to {headings[step + 1].toLowerCase()}
          </button>
        )}
      </div>
      <p className="muted">
        Use classroom information only. Do not enter names or personal details.
      </p>
    </section>
  );
}
