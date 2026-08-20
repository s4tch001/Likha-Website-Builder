"use client";

import { useEffect } from "react";

interface EditorErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function EditorError({ error, reset }: EditorErrorProps) {
  useEffect(() => {
    console.error("Likha editor render failed", error);
  }, [error]);

  return (
    <main className="editor-error" role="alert">
      <h1>The editor could not be loaded.</h1>
      <p>Try reloading the embedded editor. No project data was changed.</p>
      <button type="button" onClick={reset}>
        Reload editor
      </button>
    </main>
  );
}
