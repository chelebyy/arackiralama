import DOMPurify from "dompurify";

const ALLOWED_TAGS = [
  "p",
  "br",
  "strong",
  "em",
  "u",
  "s",
  "ul",
  "ol",
  "li",
  "blockquote",
  "h3",
  "h4",
  "a",
];

const ALLOWED_ATTR = ["href", "target", "rel"];
const FORBID_TAGS = ["script", "style", "iframe", "img", "object", "embed"];
const FORBID_ATTR = ["style", "class", "id"];
const SAFE_EXTERNAL_LINK = /^https?:/i;
const SAFE_CONTACT_LINK = /^(mailto|tel):/i;
const HAS_PROTOCOL = /^[a-z][a-z0-9+.-]*:/i;

type ManagedDOMPurify = {
  sanitize: (
    value: string,
    config: {
      ALLOWED_TAGS: string[];
      ALLOWED_ATTR: string[];
      FORBID_TAGS: string[];
      FORBID_ATTR: string[];
    },
  ) => string;
};

type DOMPurifyFactory = ((window: Window) => ManagedDOMPurify) & Partial<ManagedDOMPurify>;

function getDOMPurify() {
  if (typeof window === "undefined") {
    return null;
  }

  const purifier = DOMPurify as unknown as DOMPurifyFactory;

  if (typeof purifier.sanitize === "function") {
    return purifier as ManagedDOMPurify;
  }

  return purifier(window);
}

function getSafeHrefType(href: string) {
  const trimmedHref = href.trim();

  if (!trimmedHref || trimmedHref.startsWith("//")) {
    return null;
  }

  if (SAFE_EXTERNAL_LINK.test(trimmedHref)) {
    return "http";
  }

  if (SAFE_CONTACT_LINK.test(trimmedHref) || !HAS_PROTOCOL.test(trimmedHref)) {
    return "non-http";
  }

  return null;
}

export function sanitizeManagedHtml(value: string): string {
  const purifier = getDOMPurify();

  if (!purifier) {
    return "";
  }

  const clean = purifier.sanitize(value, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    FORBID_TAGS,
    FORBID_ATTR,
  });

  const template = document.createElement("template");
  template.innerHTML = clean;

  template.content.querySelectorAll("a").forEach((link) => {
    const hrefType = getSafeHrefType(link.getAttribute("href") ?? "");

    if (!hrefType) {
      link.removeAttribute("href");
      link.removeAttribute("target");
      link.removeAttribute("rel");
      return;
    }

    link.setAttribute("rel", "noopener noreferrer");

    if (hrefType === "http") {
      link.setAttribute("target", "_blank");
    } else {
      link.removeAttribute("target");
    }
  });

  return template.innerHTML;
}
