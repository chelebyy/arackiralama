import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/tr.json";

import { LoginForm } from "@/components/auth/login-form";

const pushMock = vi.fn();
const refreshMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: pushMock,
    refresh: refreshMock
  }),
  useSearchParams: () => new URLSearchParams()
}));

describe("LoginForm", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    pushMock.mockReset();
    refreshMock.mockReset();
  });

  it("submits admin credentials through auth login API", async () => {
    const user = userEvent.setup();

    global.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          message: "Giris basarili"
        }),
        {
          status: 200,
          headers: {
            "content-type": "application/json"
          }
        }
      )
    ) as typeof fetch;

    render(
      <NextIntlClientProvider locale="tr" messages={messages}>
        <LoginForm
          principalScope="Admin"
          title="Admin Login"
          description="desc"
          successRedirect="/dashboard/default"
        />
      </NextIntlClientProvider>
    );

    await user.type(screen.getByLabelText(/e-posta/i), "admin@example.com");
    await user.type(screen.getByLabelText(/şifre/i), "Passw0rd!");
    await user.click(screen.getByRole("button", { name: /giriş yap/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/auth/login",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            principalScope: "Admin",
            email: "admin@example.com",
            password: "Passw0rd!"
          })
        })
      );
    });

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith("/dashboard/default");
      expect(refreshMock).toHaveBeenCalled();
    });
  });
});
