import { test, expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { randomUUID } from "node:crypto";
const fixture = () =>
  JSON.parse(readFileSync(".local/e2e/fixture.json", "utf8"));
async function login(page: Page, role = "Teacher") {
  const data = fixture();
  const account = role === "PlatformAdmin" ? data.admin : data.roles[role];
  await page.goto("/login");
  await page.getByLabel("Email address").fill(account.email);
  await page.getByLabel(/^Password/).fill(account.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.locator("#main")).toBeVisible();
}

test("platform administrator explicitly enables a published teacher guest preview and learners cannot see it", async ({
  page,
  browser,
}) => {
  await login(page, "PlatformAdmin");
  const resource = await page.evaluate(async () => {
    const response = await fetch("/api/content?status=Published&pageSize=1", {
      cache: "no-store",
    });
    return (await response.json()).items[0];
  });
  await page.goto(`/app/content/${resource.id}`);
  const guestControls = page.locator("section").filter({
    has: page.getByRole("heading", { name: "Guest access", exact: true }),
  });
  await guestControls
    .getByText("Teacher guests: Disabled", { exact: true })
    .locator("..")
    .getByRole("button", { name: "Enable", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(
    guestControls.getByText("Teacher guests: Enabled", { exact: true }),
  ).toBeVisible();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const guest = await context.newPage();
  await guest.goto("/guest?audience=teacher");
  await expect(
    guest.getByRole("heading", { name: resource.title, exact: true }),
  ).toBeVisible();
  await guest
    .getByRole("button", { name: "Preview resource", exact: true })
    .click();
  await expect(
    guest.getByText(
      "Preview only. A licensed account is needed to use this resource.",
    ),
  ).toBeVisible();
  await guest
    .getByRole("button", { name: "Guest learner", exact: true })
    .click();
  await expect(
    guest.getByRole("heading", { name: resource.title, exact: true }),
  ).toHaveCount(0);
  await context.close();
  await guestControls
    .getByText("Teacher guests: Enabled", { exact: true })
    .locator("..")
    .getByRole("button", { name: "Disable", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(
    guestControls.getByText("Teacher guests: Disabled", { exact: true }),
  ).toBeVisible();
});

test("approved agreement enables adult account requests and authenticated teacher-managed learner requests without automatic licensing", async ({
  page,
  browser,
}) => {
  await login(page, "PlatformAdmin");
  await page.goto("/app/users");
  await page
    .getByText("Public registration and agreements", { exact: true })
    .click();
  const version = "browser-policy-" + randomUUID();
  await page.getByLabel("New agreement version").fill(version);
  await page.getByLabel("Agreement title").fill("Synthetic browser agreement");
  await page
    .getByLabel("Approved agreement text")
    .fill(
      "Synthetic test-only policy for the isolated browser test database. This text is not a real registration agreement and must never be published to the actual application.",
    );
  await page
    .getByLabel(/Approved source URL/)
    .fill("https://example.test/policy");
  await page.getByLabel(/Allow authorised adults to register learners/).check();
  await page
    .getByLabel(
      "This wording and registration policy are approved for Mwabu Learn v2",
    )
    .check();
  await page
    .getByRole("button", { name: "Publish approved agreement", exact: true })
    .click();
  await expect(
    page.getByText(`Current version: ${version}`, { exact: true }),
  ).toBeVisible();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const adult = await context.newPage();
  const email = `request.${randomUUID()}@mwabu.invalid`;
  await adult.goto("/register");
  await adult.getByLabel("First name").fill("BrowserAdult");
  await adult.getByLabel("Last name").fill("Registration");
  await adult.getByLabel("Email address").fill(email);
  await adult
    .getByLabel("Choose a password")
    .fill(fixture().roles.Teacher.password);
  await adult.getByLabel("Account type").selectOption("Parent");
  await expect(
    adult.getByLabel("Account type").locator('option[value="Learner"]'),
  ).toHaveCount(0);
  await adult
    .getByLabel(/I have read and accept Synthetic browser agreement/)
    .check();
  await adult
    .getByRole("button", { name: "Create account request", exact: true })
    .click();
  await expect(
    adult.getByRole("heading", {
      name: "Your request has been saved",
      exact: true,
    }),
  ).toBeVisible();
  await context.close();
  await page.reload();
  await page.getByPlaceholder("Search name or email…").fill(email);
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await page
    .getByRole("link", { name: "BrowserAdult Registration", exact: true })
    .click();
  await expect(page.getByText("Not licensed", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: "Activate licence", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Confirm", exact: true })
    .click();
  await expect(page.getByText("Licensed", { exact: true })).toBeVisible();
  await login(page, "Teacher");
  await page.goto("/app/register-learner");
  await page
    .getByLabel("Learner's school or organisation")
    .selectOption(fixture().schoolId);
  await page.getByLabel("Learner first name").fill("Managed");
  await page.getByLabel("Learner last name").fill("BrowserLearner");
  await page
    .getByLabel("Learner account email")
    .fill(`managed.${randomUUID()}@mwabu.invalid`);
  await page
    .getByLabel("Initial learner password")
    .fill(fixture().roles.Teacher.password);
  await page.getByLabel(/I am authorised to register this learner/).check();
  await page.getByLabel(/I accept Synthetic browser agreement/).check();
  await page
    .getByRole("button", {
      name: "Submit learner registration request",
      exact: true,
    })
    .click();
  await expect(
    page.getByText(/Learner request saved. Mwabu will review it/),
  ).toBeVisible();
});
