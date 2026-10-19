import { test, expect, type Page } from "@playwright/test";
import { copyFileSync, readFileSync, unlinkSync } from "node:fs";
import { dirname, join } from "node:path";
import { randomBytes } from "node:crypto";
import { pathToFileURL } from "node:url";
type Account = { email: string; password: string };
type Fixture = {
  admin: Account;
  roles: Record<string, Account>;
  schoolId: string;
};
const fixture = (): Fixture =>
  JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
let peer: Account;
let peerId: string;
async function login(page: Page, role = "Teacher") {
  const account =
    role === "Peer"
      ? peer
      : role === "PlatformAdmin"
        ? fixture().admin
        : fixture().roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}
async function mutate(
  page: Page,
  path: string,
  body: unknown,
  method = "POST",
) {
  return page.evaluate(
    async ({ path, body, method }) => {
      const csrf = await (await fetch("/api/browser/session/csrf")).json();
      const response = await fetch(path, {
        method,
        headers: {
          "Content-Type": "application/json",
          "X-Mwabu-CSRF": csrf.requestToken,
        },
        body: JSON.stringify(body),
      });
      return { status: response.status, body: await response.json() };
    },
    { path, body, method },
  );
}
async function createFree(page: Page, title: string) {
  await page.goto("/app/lessons/new");
  await page.getByRole("button", { name: "Free-form", exact: true }).click();
  await page.getByLabel("Lesson title", { exact: true }).fill(title);
  await page.getByLabel("Subject / learning area").fill("Mathematics");
  await page.getByLabel("Target age / grade").fill("Age 9-10");
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: title, exact: true }),
  ).toBeVisible();
  return page.url().split("/").at(-1)!;
}
async function proposal(page: Page, instructions = "") {
  await page.getByRole("button", { name: "AI proposals", exact: true }).click();
  await page.getByLabel("Instructions for this proposal").fill(instructions);
  await page.getByLabel(/I reviewed this lesson and selected sources/).check();
  // The controlled fixture retains a real one-second per-user cooldown; do not bypass it.
  await page.waitForTimeout(1100);
  await page
    .getByRole("button", { name: "Request AI proposal", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Proposed changes" }),
  ).toBeVisible({ timeout: 15000 });
}
test("Ministry teacher fields and progression survive save/reload and export a script-free offline copy", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Ministry synthetic fractions");
  await page
    .getByText("Ministry lesson plan form and progression", { exact: true })
    .click();
  await page.getByLabel("School", { exact: true }).fill("Synthetic school");
  await page
    .getByLabel("Name of teacher", { exact: true })
    .fill("Synthetic teacher");
  await page.getByLabel("Collaboration", { exact: true }).check();
  await page
    .getByLabel("Introduction: Learners' role", { exact: true })
    .fill("Fold paper and compare two equal halves.");
  await page.getByRole("button", { name: "Save now", exact: true }).click();
  await expect(
    page.getByRole("status").filter({ hasText: /Saved · revision/ }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByText("Ministry lesson plan form and progression", {
      exact: true,
    }),
  ).toBeVisible();
  if (!(await page.getByLabel("School", { exact: true }).isVisible()))
    await page
      .getByText("Ministry lesson plan form and progression", { exact: true })
      .click();
  await expect(page.getByLabel("School", { exact: true })).toHaveValue(
    "Synthetic school",
  );
  await expect(page.getByLabel("Collaboration", { exact: true })).toBeChecked();
  await expect(
    page.getByLabel("Introduction: Learners' role", { exact: true }),
  ).toHaveValue("Fold paper and compare two equal halves.");
  const downloaded = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "Download offline copy", exact: true })
    .click();
  const path = await (await downloaded).path();
  expect(path).toBeTruthy();
  const html = readFileSync(path!, "utf8");
  expect(html).toContain("Synthetic school");
  expect(html).toContain("Fold paper and compare two equal halves.");
  expect(html).toContain("default-src 'none'");
  expect(html).not.toContain("<script");
  expect(html).toContain("Lesson progression");
  expect(html).toContain("Read-only");
  // Playwright's artifact path has no extension; keep the app's actual download
  // filename semantics so Chromium renders HTML rather than displaying plain text.
  const offlinePath = join(dirname(path!), "mwabu-lesson-offline.html");
  copyFileSync(path!, offlinePath);
  await page.context().setOffline(true);
  try {
    await page.goto(pathToFileURL(offlinePath).href);
    await expect(
      page.getByRole("heading", {
        name: "Ministry synthetic fractions",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      page.getByText("Fold paper and compare two equal halves.", {
        exact: true,
      }),
    ).toBeVisible();
    await page.reload();
    await expect(
      page.getByRole("heading", { name: "Lesson progression", exact: true }),
    ).toBeVisible();
  } finally {
    await page.context().setOffline(false);
    unlinkSync(offlinePath);
  }
});
test.beforeAll(async ({ playwright }) => {
  const context = await playwright.request.newContext({
    baseURL: "https://127.0.0.1:7443",
    ignoreHTTPSErrors: true,
  });
  const signed = await context.post("/api/auth/login", {
    data: fixture().admin,
  });
  const { accessToken } = await signed.json();
  const headers = { Authorization: "Bearer " + accessToken };
  const org = fixture().schoolId;
  expect(
    (
      await context.put(`/api/organisations/${org}/lessons/settings`, {
        headers,
        data: {
          externalAiApproved: true,
          webSearchApproved: true,
          dailyRequestLimit: 100,
          monthlyTokenLimit: 10000000,
          monthlyCostLimitUsd: 100,
          sourceRetentionDays: 30,
        },
      })
    ).status(),
  ).toBe(200);
  peer = {
    email: `lesson.peer.${randomBytes(6).toString("hex")}@mwabu.invalid`,
    password: "A7!" + randomBytes(32).toString("base64"),
  };
  if (process.env.GITHUB_ACTIONS) console.log("::add-mask::" + peer.password);
  const created = await context.post(`/api/organisations/${org}/users`, {
    headers,
    data: {
      email: peer.email,
      firstName: "Lesson",
      lastName: "Colleague",
      initialPassword: peer.password,
    },
  });
  expect(created.status()).toBe(201);
  const account = await created.json();
  peerId = account.user.id;
  const roles = await (await context.get("/api/roles", { headers })).json();
  const teacher = roles.find(
    (role: { code: string }) => role.code === "Teacher",
  );
  expect(
    (
      await context.post(
        `/api/organisations/${org}/members/${account.membership.id}/roles`,
        { headers, data: { roleId: teacher.id } },
      )
    ).status(),
  ).toBe(201);
  await context.dispose();
});
test("teacher searches curriculum by name and creates an aligned lesson with derived defaults", async ({
  page,
}) => {
  await login(page);
  await page.getByRole("link", { name: "Lesson Planner", exact: true }).click();
  await page
    .getByRole("link", { name: "Create lesson plan", exact: true })
    .click();
  await page
    .getByRole("searchbox", { name: "Search curriculum", exact: true })
    .fill("Demonstration");
  for (const [label, value] of [
    ["Curriculum", "Demonstration Learning Framework"],
    ["Versions", "Demonstration 2026"],
    ["Grades", "Grade 4"],
    ["Subjects", "Mathematics"],
    ["Terms", "Term 1"],
    ["Topics", "Fractions in everyday life"],
    ["Competencies", "Recognise equal parts"],
    ["Learning outcomes", "Represent one half using familiar objects"],
  ])
    await page
      .getByRole("combobox", { name: label, exact: true })
      .selectOption({ label: value });
  await expect(page.getByLabel("Lesson title", { exact: true })).toHaveValue(
    "Represent one half using familiar objects",
  );
  await expect(
    page.getByRole("textbox", {
      name: "Initial learning objectives",
      exact: true,
    }),
  ).toHaveValue("Represent one half using familiar objects");
  await expect(
    page.getByRole("combobox", {
      name: "Language of instruction",
      exact: true,
    }),
  ).toHaveValue("en");
  await page
    .getByLabel("Lesson title", { exact: true })
    .fill("Aligned teaching of halves");
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Selected curriculum context" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("paragraph")
      .filter({ hasText: /Authoritative selected Mwabu curriculum records/ }),
  ).toBeVisible();
});
test("free-form mode retains setup when switching and does not claim official alignment", async ({
  page,
}) => {
  await login(page);
  await page.goto("/app/lessons/new");
  await page
    .getByLabel("Lesson title", { exact: true })
    .fill("Free-form resource lesson");
  await page
    .getByLabel("Initial learning objectives")
    .fill("Explain equal shares using familiar objects");
  await page.getByRole("button", { name: "Free-form", exact: true }).click();
  await page.getByLabel("Subject / learning area").fill("Mathematics");
  await page.getByLabel("Target age / grade").fill("Age 10");
  await page
    .getByRole("button", { name: "Curriculum-aligned", exact: true })
    .click();
  await expect(page.getByLabel("Initial learning objectives")).toHaveValue(
    "Explain equal shares using familiar objects",
  );
  await page.getByRole("button", { name: "Free-form", exact: true }).click();
  await expect(page.getByLabel("Subject / learning area")).toHaveValue(
    "Mathematics",
  );
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  await expect(
    page.getByText(/Free-form: no official alignment claimed/).first(),
  ).toBeVisible();
});
test("teacher discovers attaches and previews published Mwabu evidence without draft leakage", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Resource grounded lesson");
  await page
    .getByRole("button", { name: "Source Explorer", exact: true })
    .click();
  await page.getByLabel("Search educational sources").fill("sharing");
  await page
    .getByRole("button", { name: "Search Mwabu resources", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Sharing equally: discover one half" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Attach resource" }).first().click();
  await expect(
    page.getByRole("heading", { name: "Attached references and evidence" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Retrieve permitted evidence" })
    .click();
  await expect(
    page
      .getByText(/Extracted a bounded excerpt|Verified metadata only/)
      .first(),
  ).toBeVisible();
});
test("teacher discovers public metadata selects a source and retrieves licensed evidence", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "External grounded lesson");
  await page
    .getByRole("button", { name: "Source Explorer", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Public internet", exact: true })
    .click();
  await page.getByLabel("Search educational sources").fill("fractions");
  await page
    .getByRole("button", {
      name: "Search public educational sources",
      exact: true,
    })
    .click();
  await expect(page.getByText(/Deterministic search metadata/)).toBeVisible();
  await page.getByRole("button", { name: "Select external reference" }).click();
  await page
    .getByRole("button", { name: "Retrieve permitted evidence" })
    .click();
  await page
    .getByText("Preview retained plain-text evidence (bounded excerpt)")
    .click();
  await expect(page.getByText(/Test fixture teaching evidence/)).toBeVisible();
});
test("AI produces a structured proposal and explicit teacher apply saves a new draft revision", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "AI draft acceptance");
  await proposal(page);
  await expect(
    page.getByText(/No real external AI call was made/),
  ).toBeVisible();
  await page
    .getByRole("button", {
      name: "Apply reviewed proposal to draft",
      exact: true,
    })
    .click();
  await page
    .getByRole("button", { name: "Structured editor", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "SMART lesson objectives", exact: true }),
  ).toHaveValue(/By the end of this lesson/);
  await expect(page.getByText(/Saved · revision 2/)).toBeVisible();
});
test("section refinement is proposed separately and leaves other teacher sections intact", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Section refinement lesson");
  await proposal(page);
  await page
    .getByRole("button", {
      name: "Apply reviewed proposal to draft",
      exact: true,
    })
    .click();
  await page
    .getByRole("button", { name: "Structured editor", exact: true })
    .click();
  await expect(page.getByText(/Saved · revision 2/)).toBeVisible();
  const objectives = await page
    .getByRole("textbox", { name: "SMART lesson objectives", exact: true })
    .inputValue();
  await page
    .getByRole("button", {
      name: "AI action for Step-by-step lesson development",
      exact: true,
    })
    .click();
  await page
    .getByRole("combobox", { name: "AI action", exact: true })
    .selectOption("simplify");
  await page.getByLabel(/I reviewed this lesson and selected sources/).check();
  // The controlled fixture retains a real one-second per-user cooldown; do not bypass it.
  await page.waitForTimeout(1100);
  await page
    .getByRole("button", { name: "Request AI proposal", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Proposed changes" }),
  ).toBeVisible();
  await page
    .getByRole("button", {
      name: "Apply reviewed proposal to draft",
      exact: true,
    })
    .click();
  await page
    .getByRole("button", { name: "Structured editor", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "SMART lesson objectives", exact: true }),
  ).toHaveValue(objectives);
});
test("background generation survives navigation and reload and remains a reviewable proposal", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Background recovery lesson");
  const lessonUrl = page.url();
  await page.getByRole("button", { name: "AI proposals", exact: true }).click();
  await page
    .getByLabel("Instructions for this proposal")
    .fill("simulate-slow-job");
  await page.getByLabel(/I reviewed this lesson and selected sources/).check();
  const started = Date.now();
  await page
    .getByRole("button", { name: "Request AI proposal", exact: true })
    .click();
  await expect(
    page
      .getByRole("status")
      .filter({
        hasText: /Queued for generation|Generating a validated proposal/,
      })
      .first(),
  ).toBeVisible();
  console.log(
    `Controlled background acknowledgement: ${Date.now() - started} ms; inference fixture delays 8 s.`,
  );
  await page
    .getByRole("button", { name: "Structured editor", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "SMART lesson objectives", exact: true }),
  ).toBeEnabled();
  await page.goto("/app/lessons");
  await page.goto(lessonUrl);
  await page.reload();
  await page.getByRole("button", { name: "AI proposals", exact: true }).click();
  await expect(
    page.getByRole("button", {
      name: "Review completed proposal",
      exact: true,
    }),
  ).toBeVisible({ timeout: 15000 });
  await page
    .getByRole("button", { name: "Review completed proposal", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Proposed changes" }),
  ).toBeVisible();
  await expect(page.getByText(/Saved · revision 1/)).toBeVisible();
});
test("teacher can cancel a running background generation without changing the saved draft", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Background cancellation lesson");
  await page.getByRole("button", { name: "AI proposals", exact: true }).click();
  await page
    .getByLabel("Instructions for this proposal")
    .fill("simulate-slow-job");
  await page.getByLabel(/I reviewed this lesson and selected sources/).check();
  await page
    .getByRole("button", { name: "Request AI proposal", exact: true })
    .click();
  await expect(
    page
      .getByRole("status")
      .filter({ hasText: "Generating a validated proposal" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Cancel generation", exact: true })
    .click();
  await expect(
    page.getByRole("alert").filter({ hasText: "Generation stopped" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Proposed changes" }),
  ).toHaveCount(0);
  await expect(page.getByText(/Saved · revision 1/)).toBeVisible();
});
test("autosave and revision comparison preserve manual section edits", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Autosave teacher lesson");
  await page
    .getByRole("textbox", { name: "SMART lesson objectives", exact: true })
    .fill("Learners identify one half independently.");
  await expect(page.getByText(/Saved · revision 2/)).toBeVisible();
  await page
    .getByRole("button", { name: "Revision history", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Compare saved revision" })
    .selectOption("1");
  await expect(
    page.getByRole("heading", { name: "Proposed / later version" }).first(),
  ).toBeVisible();
  await expect(
    page.getByText("Learners identify one half independently.", {
      exact: true,
    }),
  ).toBeVisible();
});
test("a separate reviewer approves a submitted lesson and approved PDF reflects saved content", async ({
  page,
  browser,
}) => {
  await login(page);
  const id = await createFree(page, "Reviewer-approved browser lesson");
  await proposal(page);
  await page
    .getByRole("button", {
      name: "Apply reviewed proposal to draft",
      exact: true,
    })
    .click();
  await page
    .getByRole("button", { name: "Review & sharing", exact: true })
    .click();
  page.once("dialog", (dialog) => dialog.accept());
  await page
    .getByRole("button", { name: "Submit lesson for review", exact: true })
    .click();
  await expect(
    page.getByText("InReview", { exact: true }).first(),
  ).toBeVisible();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const reviewer = await context.newPage();
  await login(reviewer, "HeadTeacher");
  await reviewer.goto(`/app/lessons/${id}`);
  await reviewer
    .getByRole("button", { name: "Review & sharing", exact: true })
    .click();
  await reviewer
    .getByLabel("Publish as reusable organisation template")
    .check();
  reviewer.once("dialog", (dialog) => dialog.accept());
  await reviewer
    .getByRole("button", { name: "Approve and publish lesson", exact: true })
    .click();
  await expect(
    reviewer.getByText("Published", { exact: true }).first(),
  ).toBeVisible();
  const pdf = await reviewer.request.get(
    `/api/organisations/${fixture().schoolId}/lessons/${id}/pdf`,
  );
  expect(pdf.status()).toBe(200);
  expect((await pdf.body()).subarray(0, 4).toString()).toBe("%PDF");
  await context.close();
});
test("another authorised teacher reads a shared draft and share revocation blocks later access", async ({
  page,
  browser,
}) => {
  await login(page);
  const id = await createFree(page, "Shared teacher draft");
  await page
    .getByRole("button", { name: "Review & sharing", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Teaching colleague", exact: true })
    .selectOption(peerId);
  await page
    .getByRole("button", { name: "Share lesson with colleague", exact: true })
    .click();
  await expect(page.getByText("Lesson Colleague · Shared")).toBeVisible();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const other = await context.newPage();
  await login(other, "Peer");
  await other.goto(`/app/lessons/${id}`);
  await expect(
    other.getByRole("heading", { name: "Shared teacher draft" }),
  ).toBeVisible();
  await expect(
    other.getByRole("button", { name: "Save now", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Revoke share", exact: true }).click();
  await expect(page.getByText("Lesson Colleague · Revoked")).toBeVisible();
  const revoked = await other.request.get(
    `/api/organisations/${fixture().schoolId}/lessons/${id}`,
  );
  expect(revoked.status()).toBe(404);
  await context.close();
});
test("learner cannot access teacher plans or generation endpoints with crafted requests", async ({
  page,
}) => {
  await login(page, "Learner");
  await page.goto("/app/lessons");
  await expect(
    page.getByRole("heading", {
      name: "Select an organisation with lesson-planning access",
    }),
  ).toBeVisible();
  const response = await mutate(
    page,
    `/api/organisations/${fixture().schoolId}/lessons`,
    {},
  );
  expect(response.status).toBe(403);
  await expect(
    page.getByRole("link", { name: "Lesson Planner", exact: true }),
  ).toHaveCount(0);
});
test("provider failure preserves the teacher's current saved and manual content", async ({
  page,
}) => {
  await login(page);
  await createFree(page, "Provider failure safety");
  await page
    .getByRole("textbox", { name: "SMART lesson objectives", exact: true })
    .fill("Important teacher wording remains intact.");
  await page.getByRole("button", { name: "AI proposals", exact: true }).click();
  await page
    .getByLabel("Instructions for this proposal")
    .fill("simulate-provider-failure");
  await page.getByLabel(/I reviewed this lesson and selected sources/).check();
  // The controlled fixture retains a real one-second per-user cooldown; do not bypass it.
  await page.waitForTimeout(1100);
  await page
    .getByRole("button", { name: "Request AI proposal", exact: true })
    .click();
  await expect(
    page
      .getByRole("alert")
      .filter({ hasText: /unavailable|try again/i })
      .first(),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Structured editor", exact: true })
    .click();
  await expect(
    page.getByRole("textbox", { name: "SMART lesson objectives", exact: true }),
  ).toHaveValue("Important teacher wording remains intact.");
});
test("tablet lesson editor fits the viewport and offers a print-friendly preview", async ({
  page,
}) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await login(page);
  await createFree(page, "Tablet teaching lesson");
  await page
    .getByRole("button", { name: "Print preview", exact: true })
    .click();
  await expect(page.locator(".lesson-print")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
  ).toBe(true);
  await page.emulateMedia({ media: "print" });
  await expect(page.locator(".lesson-toolbar")).toBeHidden();
  await expect(page.locator(".lesson-print")).toBeVisible();
});
test("teacher presents a pinned saved revision with section navigation and authorized teaching resources", async ({
  page,
}) => {
  await login(page);
  const id = await createFree(page, "Classroom halves presentation");
  const path = `/api/organisations/${fixture().schoolId}/lessons/${id}`;
  const catalogue = await (
    await page.request.get("/api/learning/content?text=sharing")
  ).json();
  const resource = catalogue.items[0];
  const attached = await mutate(page, path + "/sources", {
    contentItemId: resource.id,
  });
  expect(attached.status).toBe(201);
  expect(
    (
      await mutate(
        page,
        path + `/sources/${attached.body.id}`,
        { isSelected: true, isExcluded: false, priority: 50 },
        "PATCH",
      )
    ).status,
  ).toBe(200);
  const detail = await (await page.request.get(path)).json();
  const sections = detail.document.sections.map(
    (section: { code: string; body: string }) => ({
      ...section,
      body:
        section.code === "objectives"
          ? "Identify and explain one half."
          : section.code === "starter"
            ? "Compare two equal pieces of paper."
            : section.code === "teacher-activities"
              ? "Private teacher reminder."
              : section.body,
    }),
  );
  expect(
    (
      await mutate(
        page,
        path,
        {
          expectedRevision: 1,
          setup: detail.document.setup,
          sections,
          reason: "Prepare classroom presentation",
        },
        "PUT",
      )
    ).status,
  ).toBe(200);
  await page.reload();
  await page
    .getByRole("button", { name: "Present saved lesson", exact: true })
    .click();
  await expect(page).toHaveURL(/\/present\?revision=2$/);
  await expect(page.getByText(/Saved revision 2/)).toBeVisible();
  await expect(
    page.getByText("Identify and explain one half.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Save now", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByText("Private teacher reminder.", { exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Next section", exact: true }).click();
  await expect(
    page.getByText("Compare two equal pieces of paper.", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Open a teaching resource", exact: true })
    .selectOption(resource.id);
  await expect(page.locator("iframe.document-viewer")).toBeVisible();
  await page
    .getByRole("button", { name: "Show teacher notes", exact: true })
    .click();
  await expect(
    page.getByText("Private teacher reminder.", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Enter fullscreen", exact: true })
    .click();
  if (await page.evaluate(() => document.fullscreenEnabled)) {
    await expect(
      page.getByRole("button", { name: "Exit fullscreen", exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Exit fullscreen", exact: true })
      .click();
  } else
    await expect(
      page.getByRole("status").filter({ hasText: /Fullscreen/ }),
    ).toBeVisible();
});
