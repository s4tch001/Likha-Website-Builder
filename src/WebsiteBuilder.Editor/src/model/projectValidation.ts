import type { ElementNode, Project } from "./types";

const MAX_ELEMENTS = 100_000;
const MAX_DEPTH = 128;
const routeSegment = /^[A-Za-z0-9][A-Za-z0-9_-]*$/;
const cssProperty = /^(?:--[A-Za-z_][A-Za-z0-9_-]*|-?[A-Za-z][A-Za-z0-9-]*)$/;
const attributeName = /^[A-Za-z][A-Za-z0-9:_-]*$/;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isStringRecord(value: unknown): value is Record<string, string> {
  return (
    isRecord(value) &&
    Object.values(value).every((entry) => typeof entry === "string")
  );
}

export function isSafeCssPropertyName(name: string): boolean {
  return name.length <= 128 && cssProperty.test(name);
}

export function isSafeCssValue(value: string): boolean {
  if (
    value.length > 8_192 ||
    /[\0\r\n{};]/.test(value) ||
    /@import|expression\(|javascript:|vbscript:|-moz-binding|behavior:/i.test(
      value,
    )
  ) {
    return false;
  }

  for (const match of value.matchAll(/url\(([^)]*)\)/gi)) {
    const target = (match[1] ?? "").trim().replace(/^['"]|['"]$/g, "");
    const safe =
      target.startsWith("#") ||
      (/^Assets\//i.test(target) && !target.includes("..")) ||
      /^data:image\/(?:png|jpeg|gif|webp|avif);base64,/i.test(target);
    if (!safe) {
      return false;
    }
  }

  return true;
}

function isSafeAttribute(name: string, value: string): boolean {
  if (
    !attributeName.test(name) ||
    /^on/i.test(name) ||
    /^(?:style|srcdoc|formaction|xlink:href)$/i.test(name) ||
    value.length > 16_384
  ) {
    return false;
  }

  if (!/^(?:href|src|action|poster|cite)$/i.test(name)) {
    return true;
  }

  const trimmed = value.trim();
  if (!trimmed || trimmed.startsWith("//") || trimmed.includes("\\")) {
    return false;
  }
  try {
    const url = new URL(trimmed);
    return (
      url.protocol === "https:" ||
      url.protocol === "http:" ||
      (/^href$/i.test(name) &&
        (url.protocol === "mailto:" || url.protocol === "tel:"))
    );
  } catch {
    return !trimmed.split("/").includes("..");
  }
}

function validateStyleRecord(value: unknown): value is Record<string, string> {
  return (
    isStringRecord(value) &&
    Object.entries(value).every(
      ([name, entry]) => isSafeCssPropertyName(name) && isSafeCssValue(entry),
    )
  );
}

function validateNodeShape(value: unknown): value is ElementNode {
  if (!isRecord(value)) {
    return false;
  }
  const numbers = [value.x, value.y, value.width, value.height, value.rotation];
  return (
    typeof value.id === "string" &&
    value.id.length > 0 &&
    value.id.length <= 128 &&
    typeof value.type === "string" &&
    value.type.length > 0 &&
    value.type.length <= 128 &&
    numbers.every(
      (entry) => typeof entry === "number" && Number.isFinite(entry),
    ) &&
    typeof value.width === "number" &&
    value.width >= 0 &&
    typeof value.height === "number" &&
    value.height >= 0 &&
    typeof value.hidden === "boolean" &&
    typeof value.locked === "boolean" &&
    Array.isArray(value.children) &&
    value.children.length <= 10_000 &&
    isStringRecord(value.attributes) &&
    Object.entries(value.attributes).every(([name, entry]) =>
      isSafeAttribute(name, entry),
    ) &&
    validateStyleRecord(value.styles) &&
    isRecord(value.responsiveStyles) &&
    Object.values(value.responsiveStyles).every(validateStyleRecord)
  );
}

/** Runtime boundary validation for host-provided canonical project snapshots. */
export function isValidProject(value: unknown): value is Project {
  if (
    !isRecord(value) ||
    value.schemaVersion !== 2 ||
    !Array.isArray(value.pages) ||
    value.pages.length < 1
  ) {
    return false;
  }
  if (
    !Array.isArray(value.breakpoints) ||
    !Array.isArray(value.assets) ||
    !isStringRecord(value.variables)
  ) {
    return false;
  }
  if (
    typeof value.id !== "string" ||
    typeof value.name !== "string" ||
    value.pages.length > 1_000
  ) {
    return false;
  }
  if (
    !Object.entries(value.variables).every(
      ([name, entry]) =>
        isSafeCssPropertyName(name.startsWith("--") ? name : `--${name}`) &&
        isSafeCssValue(entry),
    )
  ) {
    return false;
  }
  const breakpointIds = new Set<string>();
  let baseBreakpoints = 0;
  for (const breakpoint of value.breakpoints) {
    if (
      !isRecord(breakpoint) ||
      typeof breakpoint.id !== "string" ||
      typeof breakpoint.label !== "string" ||
      typeof breakpoint.maxWidth !== "number" ||
      typeof breakpoint.isBase !== "boolean" ||
      breakpointIds.has(breakpoint.id)
    ) {
      return false;
    }
    breakpointIds.add(breakpoint.id);
    baseBreakpoints += breakpoint.isBase ? 1 : 0;
  }
  if (
    baseBreakpoints !== 1 ||
    value.assets.length > 10_000 ||
    !value.assets.every(
      (asset) =>
        isRecord(asset) &&
        typeof asset.id === "string" &&
        typeof asset.name === "string" &&
        typeof asset.storedFileName === "string" &&
        typeof asset.relativePath === "string" &&
        asset.relativePath === `Assets/${asset.storedFileName}` &&
        typeof asset.sha256 === "string" &&
        /^[a-fA-F0-9]{64}$/.test(asset.sha256),
    )
  ) {
    return false;
  }

  const pageIds = new Set<string>();
  const routes = new Set<string>();
  const elementIds = new Set<string>();
  let count = 0;
  for (const page of value.pages) {
    if (
      !isRecord(page) ||
      typeof page.id !== "string" ||
      typeof page.route !== "string" ||
      !isRecord(page.root)
    ) {
      return false;
    }
    const route = page.route.replace(/^\/+|\/+$/g, "");
    if (
      pageIds.has(page.id) ||
      routes.has(route.toLowerCase()) ||
      !route.split("/").every((part) => routeSegment.test(part))
    ) {
      return false;
    }
    pageIds.add(page.id);
    routes.add(route.toLowerCase());

    const stack: Array<{ node: unknown; depth: number }> = [
      { node: page.root, depth: 0 },
    ];
    while (stack.length > 0) {
      const current = stack.pop();
      if (
        !current ||
        current.depth > MAX_DEPTH ||
        !validateNodeShape(current.node) ||
        ++count > MAX_ELEMENTS
      ) {
        return false;
      }
      if (elementIds.has(current.node.id)) {
        return false;
      }
      elementIds.add(current.node.id);
      for (const child of current.node.children) {
        stack.push({ node: child, depth: current.depth + 1 });
      }
    }
  }

  return true;
}
