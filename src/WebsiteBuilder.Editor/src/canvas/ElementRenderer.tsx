/* eslint-disable @next/next/no-img-element -- the editor needs native media elements for arbitrary canvas geometry and its WebView2 asset origin */
import { memo, useContext, type CSSProperties } from "react";
import type { ElementNode } from "../model/types";
import { effectiveStyles } from "../model/responsive";
import { CanvasRenderContext } from "./CanvasContext";
import { geometryStyle, toReactStyle } from "./styleUtils";
import { elementRenderEqual } from "./renderOptimization";

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
function ElementRenderer({
  node,
  isRoot,
  frameMinHeight,
}: ElementRendererProps) {
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

  if (!isRoot && node.rotation) {
    style.transform = `rotate(${node.rotation}deg)`;
  }

  const managedUrl = (attribute: "src" | "href") => {
    const value = node.attributes[attribute];
    if (!value) return undefined;
    if (!value.startsWith("Assets/")) return value;
    return ctx.assetUrls.get(value);
  };
  const common = {
    id: `wb-element-${node.id}`,
    className: "wb-element",
    "data-element-type": node.type,
    "data-element-id": node.id,
    "data-root": isRoot ? "true" : undefined,
    style,
  };

  if (node.type === "Image") {
    return (
      <img
        {...common}
        src={managedUrl("src")}
        alt={node.attributes.alt ?? node.name ?? ""}
        draggable={false}
      />
    );
  }
  if (node.type === "Video") {
    return (
      <video
        {...common}
        src={managedUrl("src")}
        controls
        preload="metadata"
        draggable={false}
      />
    );
  }
  if (node.type === "Audio") {
    return (
      <audio
        {...common}
        src={managedUrl("src")}
        controls
        preload="metadata"
        draggable={false}
      />
    );
  }
  if (node.type === "Link") {
    return (
      <a
        {...common}
        href={managedUrl("href")}
        download={node.attributes.download || undefined}
        onClick={(event) => event.preventDefault()}
        draggable={false}
      >
        {node.text ? (
          <span className="wb-element-text">{node.text}</span>
        ) : null}
        {node.children.map((child) => (
          <ElementRenderer key={child.id} node={child} />
        ))}
      </a>
    );
  }
  if (node.type === "Button") {
    return (
      <button
        {...common}
        type={node.attributes.type === "submit" ? "submit" : "button"}
        onClick={(event) => event.preventDefault()}
      >
        {node.text ? (
          <span className="wb-element-text">{node.text}</span>
        ) : null}
      </button>
    );
  }
  if (node.type === "Input") {
    return (
      <input
        {...common}
        type={node.attributes.type ?? "text"}
        name={node.attributes.name}
        placeholder={node.attributes.placeholder}
        aria-label={node.name ?? node.attributes.name ?? "Input"}
        readOnly
      />
    );
  }
  if (node.type === "Textarea") {
    return (
      <textarea
        {...common}
        name={node.attributes.name}
        placeholder={node.attributes.placeholder}
        aria-label={node.name ?? node.attributes.name ?? "Textarea"}
        readOnly
      />
    );
  }
  if (node.type === "Form") {
    return (
      <form {...common} onSubmit={(event) => event.preventDefault()}>
        {node.children.map((child) => (
          <ElementRenderer key={child.id} node={child} />
        ))}
      </form>
    );
  }

  return (
    <div {...common}>
      {node.text ? <span className="wb-element-text">{node.text}</span> : null}
      {node.children.map((child) => (
        <ElementRenderer key={child.id} node={child} />
      ))}
    </div>
  );
}

function propsEqual(
  previous: Readonly<ElementRendererProps>,
  next: Readonly<ElementRendererProps>,
): boolean {
  return (
    previous.isRoot === next.isRoot &&
    previous.frameMinHeight === next.frameMinHeight &&
    elementRenderEqual(previous.node, next.node)
  );
}

export default memo(ElementRenderer, propsEqual);
