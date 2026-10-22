import { test, expect } from "@playwright/test";
test("authentication entry points preserve guest audiences, registration and recovery", async ({
  page,
}) => {
  await page.goto("/login");
  await page
    .getByRole("link", { name: "Explore as a guest teacher", exact: true })
    .click();
  await expect(page).toHaveURL(/\/guest\?audience=teacher$/);
  await expect(
    page.getByRole("heading", { name: "Explore Mwabu Learn", exact: true }),
  ).toBeVisible();
  await page.goto("/login");
  await page
    .getByRole("link", { name: "Explore as a guest learner", exact: true })
    .click();
  await expect(page).toHaveURL(/\/guest\?audience=learner$/);
  await page.goto("/login");
  await page
    .getByRole("link", { name: "Create an account", exact: true })
    .click();
  await expect(page).toHaveURL(/\/register$/);
  await expect(
    page.getByRole("heading", {
      name: "Create your Mwabu Learn account",
      exact: true,
    }),
  ).toBeVisible();
  await page.goto("/login");
  await page
    .getByRole("link", { name: "Forgot your password?", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Recover your account", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Email address")).toBeVisible();
  await expect(page.getByLabel(/^Password/)).toHaveCount(0);
  await page.getByRole("link", { name: /Return to sign in/ }).click();
  await expect(
    page.getByRole("button", { name: "Sign in", exact: true }),
  ).toBeVisible();
  await page.goto("/reset-password");
  await expect(
    page.getByRole("heading", { name: "Set a new password", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Recovery code")).toBeVisible();
  await expect(page.getByLabel(/^New password/)).toBeVisible();
});
