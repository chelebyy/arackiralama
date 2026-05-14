import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import ContactForm from "./ContactForm";

vi.mock("next-intl", () => ({
  useTranslations: (namespace?: string) => {
    if (namespace === "contactUs.form") {
      const t = ((key: string) => key) as ((key: string) => string) & {
        rich: (key: string, values: Record<string, (chunks: React.ReactNode) => React.ReactNode>) => React.ReactNode;
      };
      t.rich = (_key, values) => (
        <>
          {values.terms("terms")}
          {" & "}
          {values.privacy("privacy")}
        </>
      );
      return t;
    }

    return (key: string, values?: Record<string, unknown>) => {
      if (key === "minLength") return `minLength-${values?.min}`;
      return key;
    };
  },
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ href, children, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

describe("ContactForm", () => {

  it("shows validation errors when the required fields are submitted empty", () => {
    render(<ContactForm />);

    fireEvent.submit(screen.getByRole("button", { name: "submit" }).closest("form")!);

    expect(screen.getAllByText("required").length).toBeGreaterThan(1);
    expect(screen.getByRole("link", { name: "terms" })).toHaveAttribute("href", "/terms");
    expect(screen.getByRole("link", { name: "privacy" })).toHaveAttribute("href", "/privacy");
  });

  it("submits valid input and shows the success state", async () => {
    render(<ContactForm />);

    fireEvent.change(screen.getByLabelText(/fullName/i), { target: { value: "Ada Lovelace" } });
    fireEvent.change(screen.getByLabelText(/^email/i), { target: { value: "ada@example.com" } });
    fireEvent.change(screen.getByLabelText(/phone/i), { target: { value: "+905551234567" } });
    fireEvent.change(screen.getByLabelText(/subject/i), { target: { value: "support" } });
    fireEvent.change(screen.getByLabelText(/message/i), { target: { value: "Need help with airport pickup." } });

    fireEvent.submit(screen.getByRole("button", { name: "submit" }).closest("form")!);

    expect(screen.getByText("submitting")).toBeInTheDocument();

    expect(await screen.findByText("success", {}, { timeout: 4000 })).toBeInTheDocument();
    expect(screen.getByText("successDesc")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "sendAnother" })).toBeInTheDocument();
  }, 6000);
});
