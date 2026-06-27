import { describe, expect, it } from "vitest";

import { sanitizeManagedHtml } from "./sanitize-managed-html";

describe("sanitizeManagedHtml", () => {
  it("keeps approved rich text tags", () => {
    expect(sanitizeManagedHtml("<p>Hello <strong>world</strong></p>")).toBe(
      "<p>Hello <strong>world</strong></p>",
    );
  });

  it("removes script iframe style and event attributes", () => {
    const result = sanitizeManagedHtml(
      '<p style="color:red" onclick="alert(1)">Hello</p><script>alert(1)</script><iframe src="https://example.com"></iframe>',
    );

    expect(result).toBe("<p>Hello</p>");
  });

  it("removes unsafe and protocol-relative links", () => {
    expect(sanitizeManagedHtml('<a href="javascript:alert(1)">bad</a>')).toBe("<a>bad</a>");
    expect(sanitizeManagedHtml('<a href="data:text/html;base64,PHNjcmlwdD4=">bad</a>')).toBe("<a>bad</a>");
    expect(sanitizeManagedHtml('<a href="//example.com">bad</a>')).toBe("<a>bad</a>");
  });

  it("removes media and embedded object tags", () => {
    expect(
      sanitizeManagedHtml(
        '<p>Text</p><img src="https://example.com/x.png"><object data="x"></object><embed src="x">',
      ),
    ).toBe("<p>Text</p>");
  });

  it("keeps safe links with noopener noreferrer", () => {
    const result = sanitizeManagedHtml('<a href="https://example.com">safe</a>');

    expect(result).toContain('rel="noopener noreferrer"');
    expect(result).toContain('target="_blank"');
  });

  it("keeps mail and phone links without opening a new tab", () => {
    const result = sanitizeManagedHtml('<a href="mailto:info@example.com">mail</a><a href="tel:+902421112233">call</a>');

    expect(result).toContain('href="mailto:info@example.com"');
    expect(result).toContain('href="tel:+902421112233"');
    expect(result).toContain('rel="noopener noreferrer"');
    expect(result).not.toContain('target="_blank"');
  });
});
