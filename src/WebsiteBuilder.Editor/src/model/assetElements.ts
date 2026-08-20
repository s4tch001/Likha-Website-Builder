import { createElement } from "./elementFactory";
import type { ElementNode, Project, ProjectAsset } from "./types";

export const ASSET_DRAG_MIME = "application/x-wb-asset-id";
export const ASSET_VIRTUAL_ORIGIN = "https://project-assets.local";

export function assetFontFamily(asset: ProjectAsset): string {
  const suffix = [...asset.id]
    .filter((character) => /^[A-Za-z0-9]$/.test(character))
    .slice(0, 16)
    .join("");
  return `LikhaAsset_${suffix || "Font"}`;
}

/** Project-scoped font declarations for live canvas previews. */
export function editorFontFaceCss(project: Project): string {
  return project.assets
    .filter((asset) => asset.kind === "Font")
    .map((asset) => {
      const url = editorAssetUrl(project, asset.relativePath);
      return url
        ? `@font-face{font-family:'${assetFontFamily(asset)}';src:url('${url}');font-display:swap;}`
        : "";
    })
    .join("\n");
}

/** Resolves only canonical managed paths; arbitrary project strings never become local-file URLs. */
export function editorAssetUrl(
  project: Project,
  relativePath: string,
): string | null {
  const asset = project.assets.find(
    (candidate) =>
      candidate.relativePath === relativePath &&
      relativePath === `Assets/${candidate.storedFileName}` &&
      !candidate.storedFileName.includes("/") &&
      !candidate.storedFileName.includes("\\"),
  );
  return asset
    ? `${ASSET_VIRTUAL_ORIGIN}/${encodeURIComponent(asset.storedFileName)}`
    : null;
}

/** Builds the appropriate semantic canvas node from validated project metadata. */
export function createAssetElement(
  asset: ProjectAsset,
  x: number,
  y: number,
): ElementNode | null {
  let node: ElementNode;
  switch (asset.kind) {
    case "Image":
    case "SVG":
    case "Icon":
      node = createElement("Image", x, y);
      node.attributes = { src: asset.relativePath, alt: asset.name };
      node.styles["object-fit"] = "contain";
      break;
    case "Video":
      node = createElement("Video", x, y);
      node.attributes = {
        src: asset.relativePath,
        controls: "true",
        preload: "metadata",
      };
      break;
    case "Audio":
      node = createElement("Audio", x, y);
      node.attributes = {
        src: asset.relativePath,
        controls: "true",
        preload: "metadata",
      };
      break;
    case "Document":
      node = createElement("Link", x, y);
      node.text = asset.name;
      node.attributes = { href: asset.relativePath, download: asset.name };
      break;
    default:
      return null;
  }

  node.name = asset.name;
  return node;
}
