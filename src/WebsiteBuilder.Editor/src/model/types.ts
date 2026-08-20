/**
 * TypeScript mirror of the C# Project JSON model (WebsiteBuilder.Core.Models).
 * Field names match the camelCase JSON produced by ProjectSerializer, so a
 * project can cross the bridge with no transformation.
 */

export interface BreakpointDef {
  id: string;
  label: string;
  maxWidth: number;
  isBase: boolean;
}

export interface ElementNode {
  id: string;
  type: string;
  name?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  /** Rotation in degrees, applied around the element's center. */
  rotation: number;
  text?: string;
  attributes: Record<string, string>;
  styles: Record<string, string>;
  responsiveStyles: Record<string, Record<string, string>>;
  hidden: boolean;
  locked: boolean;
  children: ElementNode[];
}

export interface Page {
  id: string;
  name: string;
  route: string;
  root: ElementNode;
}

export interface ProjectAsset {
  id: string;
  name: string;
  storedFileName: string;
  relativePath: string;
  kind: "Image" | "SVG" | "Icon" | "Video" | "Audio" | "Font" | "Document";
  mediaType: string;
  sizeBytes: number;
  sha256: string;
  importedUtc: string;
}

export interface Project {
  schemaVersion: number;
  id: string;
  name: string;
  createdUtc: string;
  modifiedUtc: string;
  breakpoints: BreakpointDef[];
  pages: Page[];
  variables: Record<string, string>;
  assets: ProjectAsset[];
}

/** Default frame width (px) used when the active breakpoint has no upper bound. */
export const BASE_FRAME_WIDTH = 1440;

/** Resolves the design-frame width for a breakpoint. */
export function frameWidthFor(breakpoint: BreakpointDef | undefined): number {
  if (!breakpoint || breakpoint.maxWidth <= 0) {
    return BASE_FRAME_WIDTH;
  }
  return breakpoint.maxWidth;
}
