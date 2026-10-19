import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import { useSession } from "../../auth/Session";
import { Empty, ErrorNotice, QueryState } from "../../components/ui";
type Bank = { id: string; title: string; assetId: string };
type Attempt = {
  id: string;
  questionBankId: string;
  score: number;
  total: number;
  correct: boolean[];
  createdAt: string;
  expiresAt: string;
};
type Form = Bank & {
  kind: string;
  instructions: string;
  number: number;
  minimum: number;
  maximum: number;
  denominator: number;
  questions: { prompt: string; options: { value: string; text: string }[] }[];
};
export default function Assessments() {
  const session = useSession();
  const org = session.organisationId;
  const [selected, setSelected] = useState("");
  const [error, setError] = useState<unknown>();
  const client = useQueryClient();
  const base = `/api/organisations/${org}/assessments`;
  const enabled = !!org && session.can("assessments.take");
  const banks = useQuery({
    queryKey: ["assessment-banks", org],
    queryFn: ({ signal }) => api<Bank[]>(base + "/banks", { signal }),
    enabled,
  });
  const history = useQuery({
    queryKey: ["assessment-history", org],
    queryFn: ({ signal }) => api<Attempt[]>(base + "/attempts", { signal }),
    enabled,
  });
  const form = useQuery({
    queryKey: ["assessment-form", org, selected],
    queryFn: ({ signal }) => api<Form>(base + `/banks/${selected}`, { signal }),
    enabled: enabled && !!selected,
  });
  const summary = useQuery({
    queryKey: ["assessment-summary", org, selected],
    queryFn: ({ signal }) =>
      api<
        {
          learnerUserId: string;
          learnerName: string;
          attempts: number;
          bestScore: number;
          total: number;
          lastSubmittedAt: string;
        }[]
      >(base + `/banks/${selected}/summary`, { signal }),
    enabled: enabled && !!selected && session.can("assessments.report"),
  });
  if (!enabled)
    return <Empty title="Select an organisation with assessment access" />;
  return (
    <section className="panel">
      <h1>Learning assessments</h1>
      <p>
        These online attempts are marked and saved by your organisation.
        Anonymous resource practice stays separate. No answers or progress are
        saved offline. Results expire after 365 days; you can delete your own
        results sooner.
      </p>
      {!!error && <ErrorNotice error={error} />}
      <QueryState query={banks}>
        {(rows) => (
          <label>
            Choose an assessment
            <select
              value={selected}
              onChange={(e) => setSelected(e.target.value)}
            >
              <option value="">Choose…</option>
              {rows.map((bank) => (
                <option key={bank.id} value={bank.id}>
                  {bank.title}
                </option>
              ))}
            </select>
            {rows.length === 0 && (
              <span>
                No assessments yet. Teachers can add a published interactive
                resource to the question bank from its resource page.
              </span>
            )}
          </label>
        )}
      </QueryState>
      {selected && (
        <QueryState query={form}>
          {(data) => (
            <AssessmentForm
              key={`${org}:${data.id}`}
              data={data}
              base={base}
              onSaved={() => {
                void client.invalidateQueries({
                  queryKey: ["assessment-history", org],
                });
                void client.invalidateQueries({
                  queryKey: ["assessment-summary", org],
                });
              }}
            />
          )}
        </QueryState>
      )}
      {selected && session.can("assessments.manage") && (
        <button
          className="button secondary"
          onClick={() => {
            void api(base + `/banks/${selected}`, { method: "DELETE" })
              .then(() => {
                setSelected("");
                return client.invalidateQueries({
                  queryKey: ["assessment-banks", org],
                });
              })
              .catch(setError);
          }}
        >
          Retire this assessment
        </button>
      )}
      {selected && session.can("assessments.report") && (
        <section>
          <h2>Teacher performance summary</h2>
          <p>
            Up to 100 learners. These organisation results are visible only to
            authorised staff.
          </p>
          <QueryState query={summary}>
            {(rows) => (
              <table>
                <thead>
                  <tr>
                    <th>Learner</th>
                    <th>Attempts</th>
                    <th>Best result</th>
                    <th>Last submitted</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.learnerUserId}>
                      <td>{row.learnerName}</td>
                      <td>{row.attempts}</td>
                      <td>
                        {row.bestScore} / {row.total}
                      </td>
                      <td>{new Date(row.lastSubmittedAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </QueryState>
        </section>
      )}
      <h2>Your attempt history</h2>
      <QueryState query={history}>
        {(rows) => (
          <ul>
            {rows.map((row) => (
              <li key={row.id}>
                {banks.data?.find((x) => x.id === row.questionBankId)?.title ??
                  "Retired assessment"}
                : {row.score} / {row.total} ·{" "}
                {new Date(row.createdAt).toLocaleString()}{" "}
                <button
                  className="button secondary"
                  onClick={() => {
                    void api(base + `/attempts/${row.id}`, { method: "DELETE" })
                      .then(() =>
                        client.invalidateQueries({
                          queryKey: ["assessment-history", org],
                        }),
                      )
                      .catch(setError);
                  }}
                >
                  Delete result
                </button>
              </li>
            ))}
          </ul>
        )}
      </QueryState>
    </section>
  );
}
export function AssessmentForm({
  data,
  base,
  onSaved,
}: {
  data: Form;
  base: string;
  onSaved: () => void;
}) {
  const [answers, setAnswers] = useState<string[][]>(() =>
    data.questions.length
      ? data.questions.map((q) =>
          data.kind === "sequencing" ? q.options.map(() => "") : [""],
        )
      : [data.kind === "place-value-exercise" ? ["0", "0", "0", "0"] : [""]],
  );
  const [key, setKey] = useState(() => crypto.randomUUID());
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Attempt>();
  const [error, setError] = useState<unknown>();
  const change = (i: number, position: number, text: string) => {
    setAnswers((rows) =>
      rows.map((row, index) =>
        index === i ? row.map((x, p) => (p === position ? text : x)) : row,
      ),
    );
    setKey(crypto.randomUUID());
    setResult(undefined);
  };
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        setBusy(true);
        setError(undefined);
        void api<Attempt>(base + `/banks/${data.id}/attempts`, {
          method: "POST",
          body: { submissionKey: key, answers },
        })
          .then((value) => {
            setResult(value);
            onSaved();
          })
          .catch(setError)
          .finally(() => setBusy(false));
      }}
    >
      <h2>{data.title}</h2>
      <p>{data.instructions}</p>
      {!!error && <ErrorNotice error={error} />}
      <fieldset disabled={busy || !!result}>
        <legend>Assessment answers</legend>
        {data.questions.map((q, i) => (
          <fieldset key={i}>
            <legend>{q.prompt}</legend>
            {answers[i].map((answer, p) => (
              <label key={p}>
                {data.kind === "sequencing"
                  ? `Position ${p + 1}`
                  : "Your answer"}
                {data.kind === "fill-blank" ? (
                  <input
                    required
                    maxLength={300}
                    value={answer}
                    onChange={(e) => change(i, p, e.target.value)}
                  />
                ) : (
                  <select
                    required
                    value={answer}
                    onChange={(e) => change(i, p, e.target.value)}
                  >
                    <option value="">Choose…</option>
                    {q.options.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.text}
                      </option>
                    ))}
                  </select>
                )}
              </label>
            ))}
          </fieldset>
        ))}
        {!data.questions.length && (
          <>
            <p>
              {data.kind === "fraction"
                ? `Represent ${data.number} of ${data.denominator} equal parts.`
                : `Build ${data.number}.`}
            </p>
            {answers[0].map((answer, p) => (
              <label key={p}>
                {data.kind === "place-value-exercise"
                  ? ["Thousands", "Hundreds", "Tens", "Ones"][p]
                  : data.kind === "fraction"
                    ? "Number of shaded equal parts"
                    : `Position from ${data.minimum} to ${data.maximum}`}
                <input
                  required
                  type="number"
                  step={1}
                  min={data.kind === "number-line" ? data.minimum : 0}
                  max={
                    data.kind === "place-value-exercise"
                      ? 9
                      : data.kind === "fraction"
                        ? data.denominator
                        : data.maximum
                  }
                  value={answer}
                  onChange={(e) => change(0, p, e.target.value)}
                />
              </label>
            ))}
          </>
        )}
        <button className="button" type="submit">
          {busy ? "Submitting…" : "Submit assessment"}
        </button>
      </fieldset>
      {result && (
        <>
          <p role="status">
            Server marked: {result.score} of {result.total} correct.
          </p>
          <ul>
            {result.correct.map((correct, i) => (
              <li key={i}>
                Question {i + 1}:{" "}
                {correct
                  ? "Correct"
                  : "Review the teaching resource and try again"}
              </li>
            ))}
          </ul>
          <button
            type="button"
            className="button secondary"
            onClick={() => {
              setResult(undefined);
              setKey(crypto.randomUUID());
            }}
          >
            Start another attempt
          </button>
        </>
      )}
    </form>
  );
}
