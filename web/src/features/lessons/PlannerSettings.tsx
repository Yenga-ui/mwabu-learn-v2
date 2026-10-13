import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../api/client";
import { useSession } from "../../auth/Session";
import { ErrorNotice, QueryState, notify } from "../../components/ui";
import {
  lessonBase,
  type Configuration,
  type Settings,
  type Usage,
} from "./types";
export default function PlannerSettings({
  organisation,
}: {
  organisation: string;
}) {
  const session = useSession();
  const client = useQueryClient();
  const base = lessonBase(organisation);
  const usage = useQuery({
    queryKey: ["lesson-usage", organisation],
    queryFn: () => api<Usage>(base + "/usage"),
  });
  const configuration = useQuery({
    queryKey: ["lesson-configuration", organisation],
    queryFn: () => api<Configuration>(base + "/configuration"),
  });
  const settings = useQuery({
    queryKey: ["lesson-settings", organisation],
    queryFn: () => api<Settings>(base + "/settings"),
  });
  return (
    <section className="panel">
      <h2>Usage, approval and quotas</h2>
      <QueryState query={configuration}>
        {(data) => (
          <p>
            AI provider: {data.aiConfigured ? "configured" : "unconfigured"};
            organisation approval:{" "}
            {data.organisationAiApproved ? "approved" : "not approved"}. Public
            search: {data.searchConfigured ? "configured" : "unconfigured"};
            organisation approval:{" "}
            {data.organisationSearchApproved ? "approved" : "not approved"}.
            Configuration does not prove live provider availability.
          </p>
        )}
      </QueryState>
      <QueryState query={usage}>
        {(data) => (
          <dl className="lesson-usage">
            <div>
              <dt>Requests today</dt>
              <dd>
                {data.requestsToday} / {data.dailyRequestLimit}
              </dd>
            </div>
            <div>
              <dt>Monthly charged/reserved tokens</dt>
              <dd>
                {data.chargedTokensThisMonth.toLocaleString()} /{" "}
                {data.monthlyTokenLimit.toLocaleString()}
              </dd>
            </div>
            <div>
              <dt>Estimated/conservative monthly charge</dt>
              <dd>
                USD {data.estimatedCostUsdThisMonth.toFixed(4)} /{" "}
                {data.monthlyCostLimitUsd.toFixed(2)}
              </dd>
            </div>
            <div>
              <dt>Requests with uncertain/estimated usage</dt>
              <dd>{data.uncertainRequests}</dd>
            </div>
          </dl>
        )}
      </QueryState>
      <p>
        Usage is an estimate, not a provider invoice. Cancelled/lost responses
        retain a conservative quota reservation to avoid undercounting. No
        prompts or credentials appear in usage reports.
      </p>
      {session.can("lessons.configure") && (
        <QueryState query={settings}>
          {(data) => (
            <SettingsForm
              key={organisation + JSON.stringify(data)}
              initial={data}
              save={async (value) => {
                await api(base + "/settings", { method: "PUT", body: value });
                await client.invalidateQueries({
                  queryKey: ["lesson-settings", organisation],
                });
                await client.invalidateQueries({
                  queryKey: ["lesson-configuration", organisation],
                });
                await client.invalidateQueries({
                  queryKey: ["lesson-usage", organisation],
                });
              }}
            />
          )}
        </QueryState>
      )}
    </section>
  );
}
function SettingsForm({
  initial,
  save,
}: {
  initial: Settings;
  save: (value: Settings) => Promise<void>;
}) {
  const [value, setValue] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError(undefined);
        try {
          await save(value);
          notify("Organisation planner settings saved.");
        } catch (failure) {
          setError(failure);
        } finally {
          setBusy(false);
        }
      }}
    >
      <fieldset disabled={busy}>
        <legend>Organisation governance</legend>
        <p>
          Enable only after your organisation's provider/privacy review. A
          configured server key alone is not authorisation to send personal
          data.
        </p>
        <label className="checkbox">
          <input
            type="checkbox"
            checked={value.externalAiApproved}
            onChange={(e) =>
              setValue({ ...value, externalAiApproved: e.target.checked })
            }
          />
          Organisation approves external AI processing of non-personal teaching
          context
        </label>
        <label className="checkbox">
          <input
            type="checkbox"
            checked={value.webSearchApproved}
            onChange={(e) =>
              setValue({ ...value, webSearchApproved: e.target.checked })
            }
          />
          Organisation approves public search and secure source retrieval
        </label>
        <div className="lesson-grid">
          {(
            [
              ["dailyRequestLimit", "Daily request limit", 1, 10000],
              ["monthlyTokenLimit", "Monthly token limit", 1000, 100000000],
              [
                "monthlyCostLimitUsd",
                "Monthly estimated cost limit (USD)",
                1,
                100000,
              ],
              [
                "sourceRetentionDays",
                "Extracted evidence / proposal retention (days)",
                1,
                90,
              ],
            ] as const
          ).map(([key, label, min, max]) => (
            <label key={key}>
              {label}
              <input
                type="number"
                required
                min={min}
                max={max}
                value={value[key]}
                onChange={(e) =>
                  setValue({ ...value, [key]: Number(e.target.value) })
                }
              />
            </label>
          ))}
        </div>
        {!!error && <ErrorNotice error={error} />}
        <button className="button" disabled={busy}>
          Save organisation settings
        </button>
      </fieldset>
    </form>
  );
}
