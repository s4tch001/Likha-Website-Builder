import { afterEach, describe, expect, it, vi } from "vitest";
import type { BridgeMessage, WebViewHost } from "./types";

interface HostHarness {
  host: WebViewHost;
  posted: BridgeMessage[];
  receive: (message: unknown) => void;
}

function createHost(): HostHarness {
  const posted: BridgeMessage[] = [];
  let listener: ((event: { data: unknown }) => void) | undefined;
  return {
    posted,
    host: {
      postMessage: (message) => posted.push(message as BridgeMessage),
      addEventListener: (_type, next) => {
        listener = next;
      },
    },
    receive: (message) => listener?.({ data: message }),
  };
}

async function loadBridge(host?: WebViewHost) {
  vi.resetModules();
  Object.defineProperty(globalThis, "window", {
    configurable: true,
    value: host ? { chrome: { webview: host } } : {},
  });
  return (await import("./bridge")).bridge;
}

afterEach(() => {
  vi.useRealTimers();
  Reflect.deleteProperty(globalThis, "window");
});

describe("EditorBridge", () => {
  it("rejects requests when the editor is not WebView2-hosted", async () => {
    const bridge = await loadBridge();
    expect(bridge.isHosted).toBe(false);
    await expect(bridge.invoke("host.getInfo")).rejects.toThrow(
      "Not hosted in WebView2",
    );
  });

  it("correlates a successful host response with its request", async () => {
    const harness = createHost();
    const bridge = await loadBridge(harness.host);

    const result = bridge.invoke<{ name: string }>("host.getInfo", {
      probe: true,
    });
    const request = harness.posted[0]!;
    expect(request).toMatchObject({
      type: "Request",
      method: "host.getInfo",
      payload: { probe: true },
    });

    harness.receive({
      id: request.id,
      type: "Response",
      method: request.method,
      payload: { name: "Likha" },
    });
    await expect(result).resolves.toEqual({ name: "Likha" });
  });

  it("rejects host errors and ignores malformed envelopes", async () => {
    const harness = createHost();
    const bridge = await loadBridge(harness.host);
    const result = bridge.invoke("host.fail");
    const request = harness.posted[0]!;

    harness.receive({ nope: true });
    harness.receive({
      id: request.id,
      type: "Response",
      method: request.method,
      error: { code: "failure", message: "safe host error" },
    });
    await expect(result).rejects.toThrow("safe host error");
  });

  it("times out unanswered requests and removes their pending entry", async () => {
    vi.useFakeTimers();
    const harness = createHost();
    const bridge = await loadBridge(harness.host);
    const result = bridge.invoke("host.slow");

    const rejection = expect(result).rejects.toThrow("timed out");
    await vi.advanceTimersByTimeAsync(15_000);
    await rejection;

    harness.receive({
      id: harness.posted[0]!.id,
      type: "Response",
      method: "host.slow",
      payload: "late",
    });
  });

  it("publishes events and dispatches host events to subscribers", async () => {
    const harness = createHost();
    const bridge = await loadBridge(harness.host);
    const listener = vi.fn();
    const unsubscribe = bridge.on("project.load", listener);

    bridge.publish("editor.ready", { version: "test" });
    expect(harness.posted[0]).toMatchObject({
      id: "",
      type: "Event",
      method: "editor.ready",
    });

    harness.receive({
      id: "",
      type: "Event",
      method: "project.load",
      payload: { revision: 2 },
    });
    expect(listener).toHaveBeenCalledWith({ revision: 2 });
    unsubscribe();
    harness.receive({
      id: "",
      type: "Event",
      method: "project.load",
      payload: { revision: 3 },
    });
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("answers host requests, including missing and failing handlers", async () => {
    const harness = createHost();
    const bridge = await loadBridge(harness.host);
    bridge.handle("editor.echo", (payload) => ({ payload }));
    bridge.handle("editor.fail", () => {
      throw new Error("handler failed");
    });

    harness.receive({
      id: "1",
      type: "Request",
      method: "editor.echo",
      payload: "hello",
    });
    await vi.waitFor(() => expect(harness.posted).toHaveLength(1));
    expect(harness.posted[0]).toMatchObject({
      id: "1",
      type: "Response",
      payload: { payload: "hello" },
    });

    harness.receive({ id: "2", type: "Request", method: "editor.missing" });
    await vi.waitFor(() => expect(harness.posted).toHaveLength(2));
    expect(harness.posted[1]?.error?.code).toBe("not_found");

    harness.receive({ id: "3", type: "Request", method: "editor.fail" });
    await vi.waitFor(() => expect(harness.posted).toHaveLength(3));
    expect(harness.posted[2]?.error).toMatchObject({
      code: "handler_error",
      message: "handler failed",
    });
  });
});
