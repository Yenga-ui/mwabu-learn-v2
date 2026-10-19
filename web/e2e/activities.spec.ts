import { test, expect, type Page } from "@playwright/test";
import { copyFileSync, readFileSync, unlinkSync } from "node:fs";
import { randomUUID } from "node:crypto";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";
async function login(page: Page, role: string) {
  const data = JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
  const account = role === "PlatformAdmin" ? data.admin : data.roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}
async function mutation(
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
      return response.status;
    },
    { path, body, method },
  );
}
const formats = [
  "quiz",
  "place-value",
  "matching",
  "sequencing",
  "fill-blank",
  "number-line",
  "fraction",
  "place-value-exercise",
] as const;
async function completeExercise(page: Page, kind: string) {
  if (kind === "matching")
    await page
      .getByRole("combobox", { name: "Matching answer", exact: true })
      .selectOption("0");
  if (kind === "sequencing")
    for (let i = 0; i < 4; i++)
      await page
        .getByRole("combobox", { name: `Position ${i + 1}`, exact: true })
        .selectOption(String(i));
  if (kind === "fill-blank")
    await page.getByLabel("Your answer", { exact: true }).fill("one half");
  if (kind === "number-line") {
    await page.getByRole("slider").focus();
    await page.keyboard.press("Home");
    await page.keyboard.press("ArrowRight");
    await page.keyboard.press("ArrowRight");
  }
  if (kind === "fraction") {
    await page.getByRole("button", { name: "Part 1", exact: true }).click();
    await page.getByRole("button", { name: "Part 2", exact: true }).click();
  }
  if (kind === "place-value-exercise")
    await page.getByLabel("Ones", { exact: true }).fill("2");
  await page
    .getByRole("button", { name: "Check answers", exact: true })
    .click();
  await expect(
    page.getByRole("status", { name: "Practice result" }),
  ).toContainText("1 of 1 correct");
}
for (const kind of formats) {
  test(`Content Studio publishes a reviewed ${kind} activity and the learner can use its verified offline copy`, async ({
    page,
    browser,
  }) => {
    await login(page, "ContentManager");
    await page.goto("/app/content");
    await page
      .getByRole("button", { name: "Create resource", exact: true })
      .click();
    const title =
      kind === "quiz"
        ? "Offline halves practice"
        : kind === "place-value"
          ? "Grade 4 place value"
          : `Classroom ${kind}`;
    await page.getByLabel("Resource title").fill(title);
    await page.getByLabel("URL name").fill(`activity-${randomUUID()}`);
    await page
      .getByRole("dialog")
      .getByRole("button", { name: /Save|Create/ })
      .click();
    await expect(
      page.getByRole("heading", { name: title, exact: true }),
    ).toBeVisible();
    const contentId = page.url().split("/").at(-1)!;
    await page
      .getByRole("combobox", { name: "Activity format", exact: true })
      .selectOption(kind);
    if (["number-line", "fraction", "place-value-exercise"].includes(kind))
      await page.getByLabel("Number to explore", { exact: true }).fill("2");
    await page.getByLabel("Activity title", { exact: true }).fill(title);
    await page
      .getByRole("button", { name: "Preview activity", exact: true })
      .click();
    if (kind === "quiz")
      await expect(
        page.getByRole("radio", {
          name: "One of two equal parts",
          exact: true,
        }),
      ).toBeVisible();
    else if (kind === "place-value")
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "2 thousands have a value of 2000" }),
      ).toBeVisible();
    else
      await expect(
        page.getByRole("button", { name: "Check answers", exact: true }),
      ).toBeVisible();
    await page
      .getByRole("button", { name: "Save activity file", exact: true })
      .click();
    await expect(
      page.getByRole("link", { name: "teaching-activity.json", exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Submit for review", exact: true })
      .click();
    await expect(page.getByText("InReview", { exact: true })).toBeVisible();
    const reviewer = await browser.newContext({ ignoreHTTPSErrors: true });
    const reviewerPage = await reviewer.newPage();
    const learner = await browser.newContext({
      ignoreHTTPSErrors: true,
      acceptDownloads: true,
    });
    const learnerPage = await learner.newPage();
    const offline = await browser.newContext({ ignoreHTTPSErrors: true });
    let offlinePath: string | undefined;
    try {
      await login(reviewerPage, "PlatformAdmin");
      await reviewerPage.goto(`/app/content/${contentId}`);
      await reviewerPage
        .getByRole("button", { name: "Approve activity", exact: true })
        .click();
      await reviewerPage
        .getByRole("dialog")
        .getByRole("button", { name: "Confirm", exact: true })
        .click();
      await expect(
        reviewerPage.getByText("Approved", { exact: true }),
      ).toBeVisible();
      await reviewerPage
        .getByRole("button", { name: "Publish resource", exact: true })
        .click();
      await reviewerPage
        .getByRole("dialog")
        .getByRole("button", { name: "Confirm", exact: true })
        .click();
      await expect(
        reviewerPage.getByText("Published", { exact: true }).first(),
      ).toBeVisible();
      await login(learnerPage, "Learner");
      await learnerPage.goto(`/app/resources/${contentId}`);
      await expect(
        learnerPage.getByRole("heading", { name: title, exact: true }).last(),
      ).toBeVisible();
      if (kind === "quiz") {
        const teacher = await browser.newContext({ ignoreHTTPSErrors: true });
        try {
          const teacherPage = await teacher.newPage();
          await login(teacherPage, "Teacher");
          await teacherPage.goto(`/app/resources/${contentId}`);
          await teacherPage
            .getByRole("button", { name: "Use as an assessment", exact: true })
            .click();
          await expect(
            teacherPage.getByRole("button", {
              name: "Added to organisation question bank",
              exact: true,
            }),
          ).toBeVisible();
          await learnerPage.goto("/app/assessments");
          await learnerPage
            .getByRole("combobox", {
              name: "Choose an assessment",
              exact: true,
            })
            .selectOption({ label: title });
          await learnerPage
            .getByRole("combobox", { name: "Your answer", exact: true })
            .selectOption("0");
          await learnerPage
            .getByRole("button", { name: "Submit assessment", exact: true })
            .click();
          await expect(
            learnerPage
              .getByRole("status")
              .filter({ hasText: "Server marked:" }),
          ).toContainText("Server marked: 1 of 1 correct");
          await learnerPage.reload();
          await expect(
            learnerPage.getByRole("heading", {
              name: "Your attempt history",
              exact: true,
            }),
          ).toBeVisible();
          await expect(learnerPage.getByText(/1 \/ 1/)).toBeVisible();
          await teacherPage.goto("/app/assessments");
          await teacherPage
            .getByRole("combobox", {
              name: "Choose an assessment",
              exact: true,
            })
            .selectOption({ label: title });
          await expect(
            teacherPage.getByRole("heading", {
              name: "Teacher performance summary",
              exact: true,
            }),
          ).toBeVisible();
          await expect(
            teacherPage.getByRole("cell", { name: "1 / 1", exact: true }),
          ).toBeVisible();
          await learnerPage.goto(`/app/resources/${contentId}`);
        } finally {
          await teacher.close();
        }
        await learnerPage
          .getByRole("radio", { name: "One of three equal parts", exact: true })
          .check();
        await learnerPage
          .getByRole("button", { name: "Check answers", exact: true })
          .click();
        await expect(
          learnerPage.getByRole("status").filter({ hasText: "0 of 1 correct" }),
        ).toBeVisible();
      } else if (kind === "place-value") {
        await learnerPage
          .getByRole("button", { name: "Next place", exact: true })
          .click();
        await expect(
          learnerPage
            .getByRole("status")
            .filter({ hasText: "4 hundreds have a value of 400" }),
        ).toBeVisible();
      } else await completeExercise(learnerPage, kind);
      const downloadEvent = learnerPage.waitForEvent("download");
      await learnerPage
        .getByRole("button", {
          name: "Download activity for offline use",
          exact: true,
        })
        .click();
      const downloaded = await downloadEvent;
      const original = (await downloaded.path())!;
      offlinePath = join(
        dirname(original),
        `mwabu-${kind}-${randomUUID()}.html`,
      );
      copyFileSync(original, offlinePath);
      await offline.setOffline(true);
      const offlinePage = await offline.newPage();
      const network: string[] = [];
      offlinePage.on("request", (request) => {
        if (/^https?:/.test(request.url())) network.push(request.url());
      });
      await offlinePage.goto(pathToFileURL(offlinePath).href);
      await expect(
        offlinePage.getByRole("heading", { name: title, exact: true }),
      ).toBeVisible();
      if (kind === "quiz") {
        await offlinePage
          .getByRole("radio", { name: "One of two equal parts", exact: true })
          .check();
        await offlinePage
          .getByRole("button", { name: "Check answers", exact: true })
          .click();
        await expect(offlinePage.getByRole("status")).toContainText(
          "1 of 1 correct",
        );
      } else if (kind === "place-value") {
        await offlinePage
          .getByRole("button", { name: "Next place", exact: true })
          .click();
        await expect(offlinePage.getByRole("status")).toContainText("400");
        await offlinePage
          .getByRole("button", { name: "Restart explanation", exact: true })
          .click();
        await expect(offlinePage.getByRole("status")).toContainText("Step 1");
      } else await completeExercise(offlinePage, kind);
      expect(network).toEqual([]);
    } finally {
      await reviewer.close();
      await learner.close();
      await offline.close();
      if (offlinePath) unlinkSync(offlinePath);
    }
  });
}
test("Content Studio generates a curriculum-grounded proposal through the durable queue and accepts it only into a draft", async ({
  page,
  browser,
}) => {
  const admin = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const adminPage = await admin.newPage();
    await login(adminPage, "PlatformAdmin");
    const organisations = await (
      await adminPage.request.get("/api/organisations")
    ).json();
    const contentOrganisation = organisations.find(
      (x: { organisationType: string }) => x.organisationType === "Platform",
    ).id;
    expect(
      await mutation(
        adminPage,
        `/api/organisations/${contentOrganisation}/lessons/settings`,
        {
          externalAiApproved: true,
          webSearchApproved: true,
          dailyRequestLimit: 100,
          monthlyTokenLimit: 10000000,
          monthlyCostLimitUsd: 100,
          sourceRetentionDays: 30,
        },
        "PUT",
      ),
    ).toBe(200);
  } finally {
    await admin.close();
  }
  await login(page, "ContentManager");
  await page.goto("/app/content");
  await page
    .getByRole("button", { name: "Create resource", exact: true })
    .click();
  await page.getByLabel("Resource title").fill("AI activity draft");
  await page.getByLabel("URL name").fill(`ai-draft-${randomUUID()}`);
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /Save|Create/ })
    .click();
  await expect(
    page.getByRole("heading", { name: "AI activity draft", exact: true }),
  ).toBeVisible();
  const resource = page.url().split("/").at(-1)!;
  await page.goto("/app/lessons/new");
  await page
    .getByRole("button", { name: "Start with your grade", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Grade and curriculum", exact: true })
    .selectOption({
      label: "Grade 4 · Demonstration Learning Framework · Demonstration 2026",
    });
  for (const [label, name] of [
    ["Subjects", "Mathematics"],
    ["Terms", "Term 1"],
    ["Topics", "Fractions in everyday life"],
    ["Competencies", "Recognise equal parts"],
    ["Learning outcomes", "Represent one half using familiar objects"],
  ])
    await page
      .getByRole("combobox", { name: label, exact: true })
      .selectOption({ label: name });
  await page.getByRole("button", { name: "Create draft", exact: true }).click();
  await page
    .getByText("Generate interactive content from this lesson", { exact: true })
    .click();
  await page
    .getByLabel("Content instructions", { exact: true })
    .fill("Create a question about equal halves");
  await page
    .getByLabel("I confirm these instructions contain no personal data.", {
      exact: true,
    })
    .check();
  await page
    .getByRole("button", { name: "Queue content proposal", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Review content proposal", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Controlled halves quiz", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Save into a draft resource", exact: true })
    .selectOption(resource);
  await page
    .getByRole("button", { name: "Accept into draft resource", exact: true })
    .click();
  await page
    .getByRole("link", { name: "Open resource review workflow", exact: true })
    .click();
  await expect(page.getByText("Draft", { exact: true }).first()).toBeVisible();
  await expect(
    page.getByRole("link", { name: "teaching-activity.json", exact: true }),
  ).toBeVisible();
});
