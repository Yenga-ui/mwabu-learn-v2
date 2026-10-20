import { useState } from "react";
import type { LessonDocument, MinistryLessonForm, Setup } from "./types";
import { ministryForm, stageLabels } from "./MinistryForm";

const evaluationInstruction =
  "(Reflect on how the lesson went. What went well? What could you improve? Ask learners to reflect on what they enjoyed and what they found difficult.)";
export default function OfficialPlanEditor({
  document,
  disabled,
  update,
}: {
  document: LessonDocument;
  disabled: boolean;
  update: (change: (value: LessonDocument) => LessonDocument) => void;
}) {
  const [step, setStep] = useState(0);
  const form = ministryForm(document);
  const groups = [
    "Lesson information",
    "Lesson detail",
    "Lesson progression",
    "Lesson evaluation and review",
  ];
  function setup<K extends keyof Setup>(key: K, value: Setup[K]) {
    update((d) => ({ ...d, setup: { ...d.setup, [key]: value } }));
  }
  function change(key: keyof MinistryLessonForm, value: string | string[]) {
    update((d) => {
      const codes: Record<string, string> = {
        lessonGoal: "objectives",
        priorKnowledge: "prerequisites",
        references: "references",
        materials: "materials",
        evaluation: "reflection",
      };
      return {
        ...d,
        setup: {
          ...d.setup,
          ...(key === "lessonGoal" && typeof value === "string"
            ? { objectives: value }
            : {}),
          ...(key === "materials" && typeof value === "string"
            ? { availableResources: value }
            : {}),
        },
        ministryForm: { ...ministryForm(d), [key]: value },
        sections: d.sections.map((s) =>
          s.code === codes[key] && typeof value === "string"
            ? { ...s, body: value }
            : s,
        ),
      };
    });
  }
  function field(key: keyof MinistryLessonForm, label: string, limit = 4000) {
    return (
      <label key={key}>
        {label}
        <textarea
          aria-label={label}
          readOnly={
            !!document.curriculum && (key === "subject" || key === "topic")
          }
          value={String(form[key] ?? "")}
          maxLength={limit}
          onChange={(e) => change(key, e.target.value)}
        />
        {document.curriculum && (key === "subject" || key === "topic") && (
          <small>From your selected curriculum.</small>
        )}
      </label>
    );
  }
  return (
    <section className="official-plan-editor">
      <nav className="official-steps" aria-label="Lesson plan steps">
        {groups.map((label, i) => (
          <button
            type="button"
            className={step === i ? "button" : "button secondary"}
            aria-current={step === i ? "step" : undefined}
            key={label}
            onClick={() => setStep(i)}
          >
            {i + 1}. {label}
          </button>
        ))}
      </nav>
      <section className="card">
        <h2>{groups[step]}</h2>
        <p>
          Complete the fields in the official Mwabu lesson plan. Your draft
          saves automatically; check the save status above.
        </p>
        <fieldset disabled={disabled} className="official-fields">
          {step === 0 && (
            <>
              <label>
                Lesson plan title
                <input
                  value={document.setup.title}
                  maxLength={200}
                  required
                  onChange={(e) => setup("title", e.target.value)}
                />
              </label>
              {field("school", "School", 200)}
              {field("teacherName", "Name of teacher", 200)}
              {field("className", "Class", 100)}
              <label>
                Date
                <input
                  type="date"
                  value={document.setup.lessonDate ?? ""}
                  onChange={(e) => setup("lessonDate", e.target.value || null)}
                />
              </label>
              {field("lessonTime", "Time", 50)}
              <label>
                Duration
                <input
                  type="number"
                  min={5}
                  max={480}
                  value={document.setup.durationMinutes}
                  onChange={(e) =>
                    setup("durationMinutes", Number(e.target.value))
                  }
                />
                <small>Minutes</small>
              </label>
              <label>
                Total no. of learners
                <input
                  type="number"
                  min={1}
                  max={1000}
                  value={document.setup.classSize}
                  onChange={(e) => setup("classSize", Number(e.target.value))}
                />
              </label>
            </>
          )}
          {step === 1 && (
            <>
              {field("subject", "Subject", 200)}
              {field("topic", "Topic", 200)}
              {field("subTopic", "Sub-topic", 500)}
              <fieldset>
                <legend>General competencies</legend>
                <p>Tick all that apply</p>
                {[
                  "Critical thinking",
                  "Collaboration",
                  "Creativity",
                  "Innovation",
                  "Communication",
                ].map((value) => (
                  <label className="check-label" key={value}>
                    <input
                      type="checkbox"
                      checked={form.generalCompetences.includes(value)}
                      onChange={(e) =>
                        change(
                          "generalCompetences",
                          e.target.checked
                            ? [...form.generalCompetences, value]
                            : form.generalCompetences.filter(
                                (x) => x !== value,
                              ),
                        )
                      }
                    />
                    {value}
                  </label>
                ))}
              </fieldset>
              <fieldset>
                <legend>Specific competences</legend>
                {field("specificCompetenceNumber", "SC no.", 100)}
                {field("specificCompetence", "SC")}
              </fieldset>
              {field("lessonGoal", "Lesson goal")}
              {field("rationale", "Rationale")}
              {field("priorKnowledge", "Prior knowledge")}
              {field("references", "References", 8000)}
              <fieldset>
                <legend>Learning environment</legend>
                <p>Tick all that apply</p>
                {["Natural", "Artificial", "Technological"].map((label) => (
                  <label className="check-label" key={label}>
                    <input
                      type="checkbox"
                      checked={form.learningEnvironments.includes(
                        label.toLowerCase(),
                      )}
                      onChange={(e) =>
                        change(
                          "learningEnvironments",
                          e.target.checked
                            ? [
                                ...form.learningEnvironments,
                                label.toLowerCase(),
                              ]
                            : form.learningEnvironments.filter(
                                (x) => x !== label.toLowerCase(),
                              ),
                        )
                      }
                    />
                    {label}
                  </label>
                ))}
              </fieldset>
              {field("materials", "Teaching and learning materials/resources")}
              {field("expectedStandard", "Expected standard")}
            </>
          )}
          {step === 2 && (
            <>
              <p>
                Keep the seven official stages. Timing is for planning and is
                not an extra PDF column.
              </p>
              <p role="status">
                Planned time:{" "}
                {form.progression.reduce((sum, row) => sum + row.minutes, 0)} of{" "}
                {document.setup.durationMinutes} minutes. Stage timings must
                total the lesson duration before review.
              </p>
              {stageLabels.map(([code, rawLabel, section]) => {
                const label = rawLabel
                  .replace(" (optional)", "")
                  .replace("Exercise / assessment", "Exercise/assessment");
                const row = form.progression.find((x) => x.code === code) ?? {
                  code,
                  teacherActivities: "",
                  learnerActivities: "",
                  assessmentCriteria: "",
                  minutes: 0,
                };
                function stageChange(values: Partial<typeof row>) {
                  update((d) => {
                    const current = ministryForm(d);
                    return {
                      ...d,
                      ministryForm: {
                        ...current,
                        progression: stageLabels.map(([key]) =>
                          key === code
                            ? { ...row, ...values }
                            : (current.progression.find(
                                (x) => x.code === key,
                              ) ?? {
                                code: key,
                                teacherActivities: "",
                                learnerActivities: "",
                                assessmentCriteria: "",
                                minutes: 0,
                              }),
                        ),
                      },
                      sections: d.sections.map((s) =>
                        s.code === section
                          ? {
                              ...s,
                              ...(values.teacherActivities !== undefined
                                ? { body: values.teacherActivities }
                                : {}),
                              ...(values.minutes !== undefined
                                ? { minutes: values.minutes }
                                : {}),
                            }
                          : s,
                      ),
                    };
                  });
                }
                return (
                  <fieldset key={code}>
                    <legend>{label}</legend>
                    {(
                      [
                        "teacherActivities",
                        "learnerActivities",
                        "assessmentCriteria",
                      ] as const
                    ).map((key, index) => (
                      <label key={key}>
                        {
                          [
                            "Teacher activities",
                            "Learner activities",
                            "Assessment criteria",
                          ][index]
                        }
                        <textarea
                          aria-label={`${label}: ${["Teacher activities", "Learner activities", "Assessment criteria"][index]}`}
                          value={row[key]}
                          maxLength={4000}
                          onChange={(e) =>
                            stageChange({ [key]: e.target.value })
                          }
                        />
                      </label>
                    ))}
                    <label>
                      {label}: minutes
                      <input
                        type="number"
                        min={0}
                        max={document.setup.durationMinutes}
                        value={row.minutes}
                        onChange={(e) =>
                          stageChange({ minutes: Number(e.target.value) })
                        }
                      />
                    </label>
                  </fieldset>
                );
              })}
              <p role="status">
                Planned time:{" "}
                {document.sections.reduce((sum, s) => sum + s.minutes, 0)} /{" "}
                {document.setup.durationMinutes} minutes
              </p>
            </>
          )}
          {step === 3 && (
            <>
              <p>{evaluationInstruction}</p>
              {field("evaluation", "Lesson evaluation")}
              <p>
                Check the learning goal, resources and timings before sharing
                this lesson. Long PDF fields continue on labelled continuation
                pages; the original two-page template is retained.
              </p>
            </>
          )}
        </fieldset>
        <div className="actions">
          <button
            type="button"
            className="button secondary"
            disabled={step === 0}
            onClick={() => setStep(step - 1)}
          >
            Previous
          </button>
          <button
            type="button"
            className="button"
            disabled={step === 3}
            onClick={() => setStep(step + 1)}
          >
            Next
          </button>
        </div>
      </section>
    </section>
  );
}
