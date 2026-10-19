import type { LessonDocument, MinistryLessonForm } from "./types";
import { useQuery } from "@tanstack/react-query";
import { api } from "../../api/client";
import { lessonBase } from "./types";
import { ErrorNotice } from "../../components/ui";

export const stageLabels = [
  ["introduction", "Introduction", "starter"],
  ["lesson-development", "Lesson development", "development"],
  ["exercise", "Exercise / assessment", "formative-assessment"],
  ["homework", "Homework", "extension"],
  ["conclusion", "Conclusion", "conclusion"],
  ["reinforcement", "Reinforcement (optional)", "differentiation"],
  ["extension", "Extension (optional)", "follow-up"],
] as const;
export function ministryForm(document: LessonDocument): MinistryLessonForm {
  return (
    document.ministryForm ?? {
      school: "",
      teacherName: "",
      className: "",
      lessonTime: "",
      lessonGoal: "",
      rationale: "",
      expectedStandard: "",
      generalCompetences: [],
      learningEnvironments: [],
      environmentNote: "",
      evaluation: "",
      needsReview: stageLabels.map(([code]) => code),
      progression: stageLabels.map(([code, , section]) => ({
        code,
        teacherActivities:
          document.sections.find((x) => x.code === section)?.body ?? "",
        learnerActivities: "",
        assessmentCriteria: "",
        minutes:
          document.sections.find((x) => x.code === section)?.minutes ?? 0,
      })),
    }
  );
}

export function MinistryForm({
  document,
  disabled,
  update,
  organisation,
}: {
  document: LessonDocument;
  disabled: boolean;
  update: (form: MinistryLessonForm) => void;
  organisation?: string;
}) {
  const form = ministryForm(document);
  const textFields = [
    ["school", "School"],
    ["teacherName", "Name of teacher"],
    ["className", "Class"],
    ["lessonTime", "Time"],
    ["lessonGoal", "Lesson goal"],
    ["rationale", "Rationale"],
    ["expectedStandard", "Expected standard (verify against syllabus)"],
    ["environmentNote", "Learning environment notes"],
    ["evaluation", "Lesson evaluation (complete after teaching)"],
  ] as const;
  function selection(
    key: "generalCompetences" | "learningEnvironments",
    item: string,
    checked: boolean,
  ) {
    update({
      ...form,
      [key]: checked
        ? [...form[key], item]
        : form[key].filter((x) => x !== item),
    });
  }
  return (
    <section
      className="panel ministry-form"
      aria-label="Ministry lesson plan form"
    >
      <h2>Ministry lesson plan form</h2>
      {organisation && (
        <SyllabusPicker
          document={document}
          form={form}
          organisation={organisation}
          disabled={disabled}
          update={update}
        />
      )}
      <p>
        Complete the teacher facts and review the progression. Your original
        teaching sections remain below. Date, duration and total learners are in
        lesson setup; specific competences come from the selected curriculum.
      </p>
      <div className="form-grid">
        {textFields.map(([key, label]) => (
          <label key={key}>
            {label}
            <textarea
              aria-label={label}
              value={form[key]}
              disabled={disabled}
              maxLength={
                key === "className"
                  ? 100
                  : key === "lessonTime"
                    ? 50
                    : key === "school" || key === "teacherName"
                      ? 200
                      : key === "environmentNote"
                        ? 2000
                        : 4000
              }
              onChange={(e) => update({ ...form, [key]: e.target.value })}
            />
          </label>
        ))}
      </div>
      <fieldset disabled={disabled}>
        <legend>General competences</legend>
        {[
          "Critical thinking",
          "Collaboration",
          "Creativity",
          "Innovation",
          "Communication",
        ].map((item) => (
          <label key={item} className="check-label">
            <input
              type="checkbox"
              checked={form.generalCompetences.includes(item)}
              onChange={(e) =>
                selection("generalCompetences", item, e.target.checked)
              }
            />
            {item}
          </label>
        ))}
      </fieldset>
      <fieldset disabled={disabled}>
        <legend>Learning environment</legend>
        {["natural", "artificial", "technological"].map((item) => (
          <label key={item} className="check-label">
            <input
              type="checkbox"
              checked={form.learningEnvironments.includes(item)}
              onChange={(e) =>
                selection("learningEnvironments", item, e.target.checked)
              }
            />
            {item}
          </label>
        ))}
      </fieldset>
      <h3>Lesson progression</h3>
      <label className="check-label">
        <input
          type="checkbox"
          disabled={disabled}
          checked={!form.needsReview.includes("expected-standard")}
          onChange={(e) =>
            update({
              ...form,
              needsReview: e.target.checked
                ? form.needsReview.filter((x) => x !== "expected-standard")
                : [...form.needsReview, "expected-standard"],
            })
          }
        />
        I checked the expected standard against the chosen reference
      </label>
      {stageLabels.map(([code, label]) => {
        const stage = form.progression.find((x) => x.code === code) ?? {
          code,
          teacherActivities: "",
          learnerActivities: "",
          assessmentCriteria: "",
          minutes: 0,
        };
        function change(values: Partial<typeof stage>) {
          update({
            ...form,
            progression: stageLabels.map(([key]) =>
              key === code
                ? { ...stage, ...values }
                : (form.progression.find((x) => x.code === key) ?? {
                    code: key,
                    teacherActivities: "",
                    learnerActivities: "",
                    assessmentCriteria: "",
                    minutes: 0,
                  }),
            ),
          });
        }
        return (
          <fieldset key={code} disabled={disabled}>
            <legend>{label}</legend>
            {form.needsReview.includes(code) && (
              <p>Check this stage before teaching.</p>
            )}
            <div className="form-grid">
              {(
                [
                  ["teacherActivities", "Teacher's role"],
                  ["learnerActivities", "Learners' role"],
                  ["assessmentCriteria", "Assessment criteria"],
                ] as const
              ).map(([key, title]) => (
                <label key={key}>
                  {label}: {title}
                  <textarea
                    aria-label={`${label}: ${title}`}
                    value={stage[key]}
                    maxLength={4000}
                    onChange={(e) => change({ [key]: e.target.value })}
                  />
                </label>
              ))}
              <label>
                {label}: minutes
                <input
                  type="number"
                  min={0}
                  max={document.setup.durationMinutes}
                  value={stage.minutes}
                  onChange={(e) => change({ minutes: Number(e.target.value) })}
                />
              </label>
            </div>
            <label className="check-label">
              <input
                type="checkbox"
                checked={!form.needsReview.includes(code)}
                onChange={(e) =>
                  update({
                    ...form,
                    needsReview: e.target.checked
                      ? form.needsReview.filter((x) => x !== code)
                      : [...form.needsReview, code],
                  })
                }
              />
              I reviewed this stage
            </label>
          </fieldset>
        );
      })}
    </section>
  );
}

interface Reference {
  competenceCode: string;
  competence: string;
  topic: string;
  subtopic: string;
  expectedStandard: string;
  learningActivities: string[];
  provenance: string;
}
function SyllabusPicker({
  document,
  form,
  organisation,
  disabled,
  update,
}: {
  document: LessonDocument;
  form: MinistryLessonForm;
  organisation: string;
  disabled: boolean;
  update: (form: MinistryLessonForm) => void;
}) {
  const grade = document.curriculum?.grade ?? document.setup.targetLearners;
  const subject = document.curriculum?.subject ?? document.setup.subject;
  const references = useQuery({
    queryKey: ["lesson-syllabus", organisation, grade, subject],
    queryFn: ({ signal }) =>
      api<Reference[]>(
        lessonBase(organisation) +
          `/syllabus-reference?grade=${encodeURIComponent(grade)}&subject=${encodeURIComponent(subject)}`,
        { signal },
      ),
    enabled: !!grade && !!subject,
  });
  const selected = references.data?.find(
    (x) => x.competenceCode === form.referenceCompetenceCode,
  );
  return (
    <fieldset disabled={disabled}>
      <legend>Supplied 2023 syllabus reference</legend>
      <p>
        This reference is separate from your managed curriculum alignment. Check
        that it applies to this lesson; codes are matched within the grade and
        subject.
      </p>
      {!!references.error && <ErrorNotice error={references.error} />}
      <label>
        Syllabus competence reference
        <select
          aria-label="Syllabus competence reference"
          value={form.referenceCompetenceCode ?? ""}
          onChange={(e) => {
            const entry = references.data?.find(
              (x) => x.competenceCode === e.target.value,
            );
            update({
              ...form,
              referenceCompetenceCode: entry?.competenceCode ?? "",
              syllabusProvenance: entry?.provenance ?? "",
              expectedStandard:
                entry?.expectedStandard ?? form.expectedStandard,
            });
          }}
        >
          <option value="">No reference selected</option>
          {references.data?.map((x) => (
            <option key={x.competenceCode} value={x.competenceCode}>
              {x.competenceCode} — {x.topic} / {x.competence}
            </option>
          ))}
        </select>
      </label>
      {references.data?.length === 0 && (
        <p>
          No reference coverage for this grade and subject. Do not invent an
          official expected standard.
        </p>
      )}
      {selected && (
        <>
          <p>{selected.provenance}</p>
          <p>
            Suggested syllabus activities:{" "}
            {selected.learningActivities.join("; ")}
          </p>
        </>
      )}
    </fieldset>
  );
}
