/**
 * Wire types mirroring WebsiteBuilder.Bridge.BridgeMessage on the C# side.
 * Both peers serialize to this exact shape over the WebView2 postMessage channel.
 */

export type BridgeMessageType = "Request" | "Response" | "Event";

export interface BridgeError {
  code: string;
  message: string;
}

export interface BridgeMessage<TPayload = unknown> {
  /** Correlation id; pairs a response with its request. Empty for events. */
  id: string;
  type: BridgeMessageType;
  /** Method/event name (e.g. "editor.ready", "host.getInfo"). */
  method: string;
  payload?: TPayload;
  error?: BridgeError | null;
}

/** Handler the editor exposes for host-initiated requests. */
export type RequestHandler = (payload: unknown) => unknown | Promise<unknown>;

/** Listener for host-initiated events. */
export type EventListener = (payload: unknown) => void;

/** The minimal WebView2 host-object surface we depend on. */
export interface WebViewHost {
  postMessage: (message: unknown) => void;
  addEventListener: (type: "message", listener: (event: { data: unknown }) => void) => void;
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewHost };
  }
}
