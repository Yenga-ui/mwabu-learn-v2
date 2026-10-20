import { test, expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";

const fixture = () =>
  JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
async function login(page: Page, role = "Teacher") {
  const account = fixture().roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}
async function mutate(page: Page, path: string, body: unknown) {
  const result = await page.evaluate(
    async ({ path, body }) => {
      const csrf = await (await fetch("/api/browser/session/csrf")).json();
      const response = await fetch(path, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
          "X-Mwabu-CSRF": csrf.requestToken,
        },
        body: JSON.stringify(body),
      });
      return { status: response.status, body: await response.json() };
    },
    { path, body },
  );
  expect(result.status).toBe(200);
  return result.body;
}

test("teacher starts with a published resource, completes the official plan, finds and adapts a published lesson and teaches its saved journey", async ({
  page,
  browser,
}) => {
  await login(page);
  await page.goto("/app");
  await expect(
    page.getByRole("heading", { name: "What would you like to teach?" }),
  ).toBeVisible();
  await expect(
    page.getByRole("navigation", { name: "Teaching tasks" }),
  ).toBeVisible();
  await page.screenshot({
    path: "../artifacts/template-reference/teacher-home.png",
    fullPage: true,
  });
  await page
    .getByRole("navigation", { name: "Teaching tasks" })
    .getByRole("link", { name: /Learning resources/ })
    .click();
  await page
    .getByPlaceholder("Search lessons, topics, tags or descriptions…")
    .fill("sharing");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await page
    .getByRole("link", {
      name: /Sharing equally: discover one half/,
    })
    .click();
  await expect(
    page.getByRole("heading", {
      name: "Sharing equally: discover one half",
      exact: true,
    }),
  ).toBeVisible();
  await page
    .getByRole("link", {
      name: "Create lesson using this resource",
      exact: true,
    })
    .click();
  await page
    .getByRole("combobox", { name: "Grade and curriculum", exact: true })
    .selectOption({
      label: "Grade 4 · Demonstration Learning Framework · Demonstration 2026",
    });
  for (const [label, value] of [
    ["Subjects", "Mathematics"],
    ["Terms", "Term 1"],
    ["Topics", "Fractions in everyday life"],
    ["Competencies", "Recognise equal parts"],
    ["Learning outcomes", "Represent one half using familiar objects"],
  ])
    await page
      .getByRole("combobox", { name: label, exact: true })
      .selectOption({ label: value });
  const title = "Teacher acceptance: Grade 4 fractions";
  await page.getByLabel("Lesson title", { exact: true }).fill(title);
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: title, exact: true }),
  ).toBeVisible();
  const id = page.url().split("/").at(-1)!;
  const path = `/api/organisations/${fixture().schoolId}/lessons/${id}`;
  await expect(
    page.getByText("More lesson tools", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "AI proposals", exact: true }),
  ).not.toBeVisible();
  await page
    .getByLabel("School", { exact: true })
    .fill("Synthetic acceptance school");
  await page.getByLabel("Class", { exact: true }).fill("Grade 4");
  await page
    .getByRole("heading", { name: title, exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({
    path: "../artifacts/template-reference/official-editor.png",
    fullPage: true,
  });
  await page
    .getByRole("button", { name: "2. Lesson detail", exact: true })
    .click();
  await expect(page.getByLabel("Subject", { exact: true })).toHaveAttribute(
    "readonly",
    "",
  );
  await page
    .getByLabel("Sub-topic", { exact: true })
    .fill("Equal sharing acceptance");
  await page
    .getByLabel("Lesson goal", { exact: true })
    .fill("Recognise two equal parts.");
  await page
    .getByLabel("Rationale", { exact: true })
    .fill("Use familiar examples of fair sharing.");
  await page
    .getByLabel("Expected standard", { exact: true })
    .fill("Identify one half correctly.");
  await page
    .getByRole("button", { name: "3. Lesson progression", exact: true })
    .click();
  for (const stage of [
    "Introduction",
    "Lesson development",
    "Exercise/assessment",
    "Homework",
    "Conclusion",
    "Reinforcement",
    "Extension",
  ]) {
    await page
      .getByLabel(`${stage}: Teacher activities`, { exact: true })
      .fill("Guide a discussion of equal parts.");
    await page
      .getByLabel(`${stage}: Learner activities`, { exact: true })
      .fill("Compare equal parts.");
    await page
      .getByLabel(`${stage}: Assessment criteria`, { exact: true })
      .fill("Recognises equal parts.");
  }
  await page.getByLabel("Introduction: minutes", { exact: true }).fill("5");
  await page
    .getByLabel("Lesson development: minutes", { exact: true })
    .fill("25");
  await page
    .getByLabel("Exercise/assessment: minutes", { exact: true })
    .fill("5");
  await page.getByLabel("Conclusion: minutes", { exact: true }).fill("5");
  await page.getByRole("button", { name: "Save now", exact: true }).click();
  await expect(
    page.getByRole("status").filter({ hasText: /Saved · revision/ }),
  ).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("School", { exact: true })).toHaveValue(
    "Synthetic acceptance school",
  );
  await page
    .getByRole("button", { name: "View resources", exact: true })
    .click();
  await expect(
    page.getByRole("link", {
      name: "Preview Sharing equally: discover one half",
      exact: true,
    }),
  ).toBeVisible();
  let original = await (await page.request.get(path)).json();
  expect(original.document.references).toHaveLength(1);
  expect(
    original.document.teachingSteps.some(
      (s: { resources: unknown[] }) => s.resources.length > 0,
    ),
  ).toBe(true);
  original = await mutate(page, path + "/status", {
    expectedRevision: original.lesson.revision,
    status: "InReview",
    note: "Teacher checked",
  });
  const reviewerContext = await browser.newContext({ ignoreHTTPSErrors: true });
  const reviewer = await reviewerContext.newPage();
  await login(reviewer, "HeadTeacher");
  original = await mutate(reviewer, path + "/status", {
    expectedRevision: original.lesson.revision,
    status: "Published",
    note: "Independently reviewed",
    organisationTemplate: true,
  });
  await reviewerContext.close();
  await page.goto("/app");
  await page.getByLabel("Search lessons", { exact: true }).fill("fractions");
  await page
    .getByRole("button", { name: "Find a lesson", exact: true })
    .click();
  const card = page
    .locator("article")
    .filter({ has: page.getByRole("heading", { name: title, exact: true }) });
  await expect(card).toContainText("Grade 4");
  await expect(card).toContainText("Mathematics");
  await expect(card).toContainText("1 learning resources attached");
  await card.getByRole("link", { name: "View lesson", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/app/lessons/${id}$`));
  await expect(
    page.getByRole("heading", { name: title, exact: true }),
  ).toBeVisible();
  await expect(
    page.getByLabel("Lesson plan title", { exact: true }),
  ).toHaveValue(title);
  await page
    .getByRole("button", { name: "Use this lesson", exact: true })
    .click();
  await expect(
    page.getByLabel("Lesson plan title", { exact: true }),
  ).toHaveValue(title + " (copy)");
  await page
    .getByLabel("Lesson plan title", { exact: true })
    .fill("My adapted fractions lesson");
  await page.getByRole("button", { name: "Save now", exact: true }).click();
  await expect(
    page.getByRole("status").filter({ hasText: /Saved · revision/ }),
  ).toBeVisible();
  const download = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "Export saved PDF", exact: true })
    .click();
  expect((await download).suggestedFilename()).toMatch(/\.pdf$/);
  const unchanged = await (await page.request.get(path)).json();
  expect(unchanged.lesson.revision).toBe(original.lesson.revision);
  expect(unchanged.lesson.title).toBe(title);
  await page
    .getByRole("button", { name: "Prepare Lesson Journey", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Preparation readiness", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "Teach Lesson", exact: true }).click();
  await expect(page.locator(".classroom-canvas")).toBeVisible();
  await expect(page.locator(".classroom-canvas")).not.toContainText(
    "Guide a discussion",
  );
});
