import { useContext, type CSSProperties } from "react";
import type { ElementNode } from "../model/types";
import { effectiveStyles } from "../model/responsive";
import { CanvasRenderContext } from "./CanvasContext";
import { geometryStyle, toReactStyle } from "./styleUtils";

interface ElementRendererProps {
  node: ElementNode;
  isRoot?: boolean;
  /** Minimum height (px) the root element should fill, so its background covers the frame. */
  frameMinHeight?: number;
}

/**
 * Recursively renders an element tree into DOM. Each node carries its geometry,
 * styles and text, plus data attributes used for hit-testing during drag. The
 * selected element and the active drop target get outline classes, and the node
 * being dragged is offset live via a transform.
 */
export default function ElementRenderer({ node, isRoot, frameMinHeight }: ElementRendererProps) {
  const ctx = useContext(CanvasRenderContext);

  if (node.hidden) {
    return null;
  }

  const base: CSSProperties = isRoot
    ? {
        position: "relative",
        width: "100%",
        minHeight: frameMinHeight ? `${frameMinHeight}px` : "100%",
      }
    : geometryStyle(node);

  const resolved = effectiveStyles(node, ctx.breakpoints, ctx.breakpointId);
  const style: CSSProperties = { ...base, ...toReactStyle(resolved) };

  const isDragging = ctx.dragIds.includes(node.id);
  const transforms: string[] = [];
  if (isDragging) {
    transforms.push(`translate(${ctx.dragDX}px, ${ctx.dragDY}px)`);
  }
  if (!isRoot && node.rotation) {
    transforms.push(`rotate(${node.rotation}deg)`);
  }
  if (transforms.length > 0) {
    style.transform = transforms.join(" ");
  }

  const classes = ["wb-element"];
  if (ctx.selectedIds.includes(node.id)) {
    classes.push("selected");
  }
  if (node.id === ctx.dropTargetId) {
    classes.push("drop-target");
  }
  if (isDragging) {
    classes.push("dragging");
  }

  return (
    <div
      className={classes.join(" ")}
      data-element-type={node.type}
      data-element-id={node.id}
      data-root={isRoot ? "true" : undefined}
      style={style}
    >
      {node.text ? <span className="wb-element-text">{node.text}</span> : null}
      {node.children.map((child) => (
        <ElementRenderer key={child.id} node={child} />
      ))}
    </div>
  );
}
