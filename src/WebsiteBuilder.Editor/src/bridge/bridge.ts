import type {
  BridgeMessage,
  EventListener,
  RequestHandler,
  WebViewHost,
} from "./types";

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (reason: unknown) => void;
  timeoutId: ReturnType<typeof setTimeout>;
}

const REQUEST_TIMEOUT_MS = 15_000;

function isBridgeMessage(value: unknown): value is BridgeMessage {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const message = value as Partial<BridgeMessage>;
  return (
    typeof message.id === "string" &&
    typeof message.method === "string" &&
    (message.type === "Request" ||
      message.type === "Response" ||
      message.type === "Event")
  );
}

/**
 * Typed JSON-RPC client over the WebView2 postMessage channel — the editor-side
 * counterpart of the C# WebView2EditorBridge. Symmetric with the host:
 *
 *  - `invoke(method, payload)` sends a request and awaits the host's response.
 *  - `publish(method, payload)` fires a one-way event.
 *  - `handle(method, fn)` answers host-initiated requests.
 *  - `on(event, fn)` subscribes to host-initiated events.
 *
 * When the page is opened outside WebView2 (e.g. `npm run dev` in a browser),
 * `isHosted` is false and `invoke` rejects, so the UI can degrade gracefully.
 */
class EditorBridge {
  private readonly host: WebViewHost | undefined;
  private readonly pending = new Map<string, PendingRequest>();
  private readonly handlers = new Map<string, RequestHandler>();
  private readonly listeners = new Map<string, Set<EventListener>>();

  constructor() {
    this.host =
      typeof window === "undefined" ? undefined : window.chrome?.webview;
    this.host?.addEventListener("message", (event) => {
      if (isBridgeMessage(event.data)) {
        void this.onMessage(event.data);
      }
    });
  }

  /** True when running inside the WebView2 host. */
  get isHosted(): boolean {
    return this.host !== undefined;
  }

  /** Sends a request to the host and resolves with its response payload. */
  invoke<TResponse = unknown>(
    method: string,
    payload?: unknown,
  ): Promise<TResponse> {
    if (!this.host) {
      return Promise.reject(new Error("Not hosted in WebView2."));
    }

    const id = crypto.randomUUID();
    const message: BridgeMessage = { id, type: "Request", method, payload };

    return new Promise<TResponse>((resolve, reject) => {
      const timeoutId = setTimeout(() => {
        if (this.pending.delete(id)) {
          reject(new Error(`Bridge request '${method}' timed out.`));
        }
      }, REQUEST_TIMEOUT_MS);
      this.pending.set(id, {
        resolve: (value) => resolve(value as TResponse),
        reject,
        timeoutId,
      });
      try {
        this.post(message);
      } catch (error) {
        clearTimeout(timeoutId);
        this.pending.delete(id);
        reject(error);
      }
    });
  }

  /** Fires a one-way event to the host. */
  publish(method: string, payload?: unknown): void {
    this.post({ id: "", type: "Event", method, payload });
  }

  /** Registers a handler the host can call via a request. */
  handle(method: string, handler: RequestHandler): void {
    this.handlers.set(method, handler);
  }

  /** Subscribes to a host event; returns an unsubscribe function. */
  on(event: string, listener: EventListener): () => void {
    let set = this.listeners.get(event);
    if (!set) {
      set = new Set();
      this.listeners.set(event, set);
    }
    set.add(listener);
    return () => set?.delete(listener);
  }

  private post(message: BridgeMessage): void {
    this.host?.postMessage(message);
  }

  private async onMessage(message: BridgeMessage): Promise<void> {
    if (!message || typeof message.type !== "string") {
      return;
    }

    switch (message.type) {
      case "Response": {
        const pending = this.pending.get(message.id);
        if (pending) {
          this.pending.delete(message.id);
          clearTimeout(pending.timeoutId);
          if (message.error) {
            pending.reject(new Error(message.error.message));
          } else {
            pending.resolve(message.payload);
          }
        }
        break;
      }

      case "Event": {
        this.listeners
          .get(message.method)
          ?.forEach((listener) => listener(message.payload));
        break;
      }

      case "Request": {
        await this.dispatchRequest(message);
        break;
      }
    }
  }

  private async dispatchRequest(message: BridgeMessage): Promise<void> {
    const handler = this.handlers.get(message.method);

    if (!handler) {
      this.post({
        id: message.id,
        type: "Response",
        method: message.method,
        error: {
          code: "not_found",
          message: `No editor handler for '${message.method}'.`,
        },
      });
      return;
    }

    try {
      const result = await handler(message.payload);
      this.post({
        id: message.id,
        type: "Response",
        method: message.method,
        payload: result ?? null,
      });
    } catch (error) {
      this.post({
        id: message.id,
        type: "Response",
        method: message.method,
        error: {
          code: "handler_error",
          message: error instanceof Error ? error.message : String(error),
        },
      });
    }
  }
}

/** Process-wide bridge singleton. */
export const bridge = new EditorBridge();
