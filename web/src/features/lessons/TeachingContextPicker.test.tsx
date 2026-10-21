import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { expect, it, vi } from "vitest";
import { api } from "../../api/client";
import TeachingContextPicker, {
  type TeachingContext,
} from "./TeachingContextPicker";
vi.mock("../../api/client", async () => ({
  ...(await vi.importActual<typeof import("../../api/client")>(
    "../../api/client",
  )),
  api: vi.fn(),
}));

it("chooses a source grade, subject, topic and subtopic without typing and clears descendants", async () => {
  vi.mocked(api).mockResolvedValue({
    choices: [
      {
        year: 2023,
        grade: 2,
        subject: "Creative and Technology Studies",
        topic: "Recreation",
        subTopic: "Games",
        term: null,
      },
      {
        year: 2023,
        grade: 4,
        subject: "Mathematics",
        topic: "Fractions",
        subTopic: "Equal parts",
        term: "Term 1",
      },
    ],
  });
  function Harness() {
    const [context, change] = useState<TeachingContext>({
      year: "",
      grade: "",
      subject: "",
      term: "",
      topic: "",
    });
    return (
      <>
        <TeachingContextPicker value={context} change={change} />
        <output aria-label="Selection">{JSON.stringify(context)}</output>
      </>
    );
  }
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <Harness />
    </QueryClientProvider>,
  );
  fireEvent.click(await screen.findByRole("button", { name: "Grade 2" }));
  fireEvent.click(
    screen.getByRole("button", { name: "Creative and Technology Studies" }),
  );
  fireEvent.click(screen.getByRole("button", { name: "Recreation" }));
  fireEvent.click(screen.getByRole("button", { name: "Games" }));
  expect(screen.getByLabelText("Selection")).toHaveTextContent(
    '"subTopic":"Games"',
  );
  fireEvent.click(screen.getByRole("button", { name: "Grade 4" }));
  expect(
    screen.queryByRole("button", { name: "Games" }),
  ).not.toBeInTheDocument();
  expect(screen.getByLabelText("Selection")).toHaveTextContent('"topic":""');
  expect(screen.getByLabelText("Selection")).toHaveTextContent('"subject":""');
});
