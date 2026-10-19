import { useState } from "react";
import { useBeforeUnload, useBlocker } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../../api/client";
import type { Asset } from "../../../api/types";
import { ErrorNotice, QueryState, notify } from "../../../components/ui";
import {
  activityMime,
  type ActivityDefinition,
  type ActivityQuestion,
} from "./activityRuntime";
import { ActivityPlayer } from "./ActivityPlayer";
export const sampleActivity = (): ActivityDefinition => ({
  schemaVersion: 1,
  kind: "quiz",
  title: "Recognising one half",
  language: "en",
  instructions: "Choose an answer, then check it and read the explanation.",
  number: 2436,
  questions: [
    {
      prompt: "Which choice describes one half?",
      options: [
        "One of two equal parts",
        "One of three equal parts",
        "One of four equal parts",
        "Three of four equal parts",
      ],
      correctOption: 0,
      explanation: "One half is one of two equal parts of the same whole.",
    },
  ],
});

export default function ActivityComposer({
  id,
  editable,
  language,
}: {
  id: string;
  editable: boolean;
  language: string;
}) {
  const assets = useQuery({
    queryKey: ["studio-assets", id],
    queryFn: () => api<Asset[]>(`/api/content/${id}/assets`),
  });
  const asset = assets.data?.find((x) => x.mimeType === activityMime);
  const saved = useQuery({
    queryKey: ["activity-author", id, asset?.id],
    queryFn: ({ signal }) =>
      api<{ definition: ActivityDefinition }>(
        `/api/content/${id}/activities/${asset!.id}`,
        { signal },
      ),
    enabled: !!asset,
  });
  if (!editable && !asset && assets.data) return null;
  return (
    <section className="card">
      <h2>Interactive activity authoring</h2>
      <p>
        Create a practice quiz or a place-value explanation. Preview it, save
        the activity file and use the existing review and publishing workflow.
        Only published resources appear to learners.
      </p>
      {asset ? (
        <QueryState query={saved}>
          {(data) => (
            <ActivityForm
              key={asset.id}
              id={id}
              editable={editable}
              initial={data.definition}
            />
          )}
        </QueryState>
      ) : (
        <QueryState query={assets}>
          {() => (
            <ActivityForm
              key={id}
              id={id}
              editable={editable}
              initial={{ ...sampleActivity(), language }}
            />
          )}
        </QueryState>
      )}
    </section>
  );
}
function ActivityForm({
  id,
  editable,
  initial,
}: {
  id: string;
  editable: boolean;
  initial: ActivityDefinition;
}) {
  const [value, setValue] = useState(initial);
  const [questions, setQuestions] = useState<ActivityQuestion[]>(
    initial.questions.length ? initial.questions : sampleActivity().questions,
  );
  const [preview, setPreview] = useState<ActivityDefinition>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const client = useQueryClient();
  const definition = {
    ...value,
    questions: ["quiz", "matching", "sequencing", "fill-blank"].includes(
      value.kind,
    )
      ? questions
      : [],
  };
  const dirty =
    editable && JSON.stringify(definition) !== JSON.stringify(initial);
  const blocker = useBlocker(() => dirty && !busy);
  useBeforeUnload((event) => {
    if (dirty) {
      event.preventDefault();
      event.returnValue = "";
    }
  });
  const changeQuestion = (index: number, values: Partial<ActivityQuestion>) =>
    setQuestions((rows) =>
      rows.map((row, i) => (i === index ? { ...row, ...values } : row)),
    );
  return (
    <>
      {blocker.state === "blocked" && (
        <div role="alert">
          <p>Your activity changes are not saved.</p>
          <button
            type="button"
            className="button"
            onClick={() => blocker.reset()}
          >
            Keep editing activity
          </button>
          <button
            type="button"
            className="button secondary"
            onClick={() => blocker.proceed()}
          >
            Discard activity changes and leave
          </button>
        </div>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          setBusy(true);
          setError(undefined);
          void api(`/api/content/${id}/activities`, {
            method: "POST",
            body: definition,
          })
            .then(async () => {
              await client.invalidateQueries({
                queryKey: ["studio-assets", id],
              });
              notify("Activity file saved. Review before publishing.");
            })
            .catch(setError)
            .finally(() => setBusy(false));
        }}
      >
        {!!error && <ErrorNotice error={error} />}
        <fieldset disabled={!editable || busy}>
          <legend>Activity definition</legend>
          <label>
            Activity format
            <select
              value={value.kind}
              onChange={(e) =>
                setValue({
                  ...value,
                  kind: e.target.value as ActivityDefinition["kind"],
                  schemaVersion: 2,
                  number:
                    e.target.value === "fraction"
                      ? 1
                      : e.target.value === "number-line"
                        ? 5
                        : value.number,
                })
              }
            >
              <option value="quiz">Multiple-choice practice</option>
              <option value="place-value">Place-value animation</option>
              <option value="matching">Matching</option>
              <option value="sequencing">Sequencing</option>
              <option value="fill-blank">Fill in the blank</option>
              <option value="number-line">Number line</option>
              <option value="fraction">Fraction visualisation</option>
              <option value="place-value-exercise">
                Build with place values
              </option>
            </select>
          </label>
          <label>
            Activity title
            <input
              required
              maxLength={200}
              value={value.title}
              onChange={(e) => setValue({ ...value, title: e.target.value })}
            />
          </label>
          <label>
            Learner instructions
            <input
              required
              maxLength={2000}
              value={value.instructions}
              onChange={(e) =>
                setValue({ ...value, instructions: e.target.value })
              }
            />
          </label>
          {[
            "place-value",
            "number-line",
            "fraction",
            "place-value-exercise",
          ].includes(value.kind) ? (
            <>
              <label>
                Number to explore
                <input
                  type="number"
                  required
                  min={0}
                  max={9999}
                  value={value.number}
                  onChange={(e) =>
                    setValue({ ...value, number: Number(e.target.value) })
                  }
                />
              </label>
              {value.kind === "number-line" && (
                <>
                  <label>
                    Line starts at
                    <input
                      required
                      type="number"
                      min={-1000}
                      max={999}
                      value={value.minimum ?? 0}
                      onChange={(e) =>
                        setValue({ ...value, minimum: Number(e.target.value) })
                      }
                    />
                  </label>
                  <label>
                    Line ends at
                    <input
                      required
                      type="number"
                      min={1}
                      max={1000}
                      value={value.maximum ?? 20}
                      onChange={(e) =>
                        setValue({ ...value, maximum: Number(e.target.value) })
                      }
                    />
                  </label>
                </>
              )}
              {value.kind === "fraction" && (
                <label>
                  Equal parts in the whole
                  <input
                    required
                    type="number"
                    min={2}
                    max={24}
                    value={value.denominator ?? 4}
                    onChange={(e) =>
                      setValue({
                        ...value,
                        denominator: Number(e.target.value),
                      })
                    }
                  />
                </label>
              )}
            </>
          ) : (
            <>
              {value.kind === "sequencing" && (
                <p>
                  Enter answer choices in the correct order. Learners choose the
                  order themselves.
                </p>
              )}
              {value.kind === "fill-blank" && (
                <p>
                  Answer choices are accepted spellings or equivalent answers.
                  They are hidden during practice.
                </p>
              )}
              {questions.map((question, index) => (
                <fieldset key={index}>
                  <legend>Question {index + 1}</legend>
                  <label>
                    Question {index + 1}: prompt
                    <input
                      required
                      maxLength={1000}
                      value={question.prompt}
                      onChange={(e) =>
                        changeQuestion(index, { prompt: e.target.value })
                      }
                    />
                  </label>
                  {question.options.map((option, choice) => (
                    <label key={choice}>
                      Question {index + 1}: answer choice {choice + 1}
                      <input
                        required
                        maxLength={300}
                        value={option}
                        onChange={(e) =>
                          changeQuestion(index, {
                            options: question.options.map((item, i) =>
                              i === choice ? e.target.value : item,
                            ),
                          })
                        }
                      />
                    </label>
                  ))}
                  <label>
                    Question {index + 1}: correct answer
                    <select
                      value={question.correctOption}
                      onChange={(e) =>
                        changeQuestion(index, {
                          correctOption: Number(e.target.value),
                        })
                      }
                    >
                      {question.options.map((option, i) => (
                        <option key={i} value={i}>
                          {option || `Answer choice ${i + 1}`}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Question {index + 1}: explanation
                    <input
                      required
                      maxLength={2000}
                      value={question.explanation}
                      onChange={(e) =>
                        changeQuestion(index, { explanation: e.target.value })
                      }
                    />
                  </label>
                  {questions.length > 1 && (
                    <button
                      type="button"
                      className="button secondary"
                      onClick={() =>
                        setQuestions((rows) =>
                          rows.filter((_, i) => i !== index),
                        )
                      }
                    >
                      Remove question {index + 1}
                    </button>
                  )}
                </fieldset>
              ))}
              {questions.length < 10 && (
                <button
                  type="button"
                  className="button secondary"
                  onClick={() =>
                    setQuestions((rows) => [
                      ...rows,
                      sampleActivity().questions[0],
                    ])
                  }
                >
                  Add practice question
                </button>
              )}
            </>
          )}
          <button className="button" type="submit">
            {busy ? "Saving activity…" : "Save activity file"}
          </button>
        </fieldset>
      </form>
      <button
        className="button secondary"
        type="button"
        onClick={() => setPreview(structuredClone(definition))}
      >
        Preview activity
      </button>
      {preview && <ActivityPlayer definition={preview} />}
    </>
  );
}
