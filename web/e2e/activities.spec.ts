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
for (const kind of ["quiz", "place-value"] as const) {
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
      kind === "quiz" ? "Offline halves practice" : "Grade 4 place value";
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
    else
      await expect(
        page
          .getByRole("status")
          .filter({ hasText: "2 thousands have a value of 2000" }),
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
        await learnerPage
          .getByRole("radio", { name: "One of three equal parts", exact: true })
          .check();
        await learnerPage
          .getByRole("button", { name: "Check answers", exact: true })
          .click();
        await expect(
          learnerPage.getByRole("status").filter({ hasText: "0 of 1 correct" }),
        ).toBeVisible();
      } else {
        await learnerPage
          .getByRole("button", { name: "Next place", exact: true })
          .click();
        await expect(
          learnerPage
            .getByRole("status")
            .filter({ hasText: "4 hundreds have a value of 400" }),
        ).toBeVisible();
      }
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
      } else {
        await offlinePage
          .getByRole("button", { name: "Next place", exact: true })
          .click();
        await expect(offlinePage.getByRole("status")).toContainText("400");
        await offlinePage
          .getByRole("button", { name: "Restart explanation", exact: true })
          .click();
        await expect(offlinePage.getByRole("status")).toContainText("Step 1");
      }
      expect(network).toEqual([]);
    } finally {
      await reviewer.close();
      await learner.close();
      await offline.close();
      if (offlinePath) unlinkSync(offlinePath);
    }
  });
}
