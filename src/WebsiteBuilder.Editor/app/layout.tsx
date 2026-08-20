import type { Metadata } from "next";
import type { ReactNode } from "react";
import "../src/index.css";
import "../src/canvas/canvas.css";

const productionCsp = [
  "default-src 'self'",
  "base-uri 'none'",
  "connect-src 'self'",
  "font-src 'self' data:",
  "form-action 'none'",
  "frame-src 'none'",
  "img-src 'self' data: blob:",
  "object-src 'none'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "worker-src 'self' blob:",
].join("; ");

export const metadata: Metadata = {
  title: "Likha Editor",
  description: "Embedded visual editor for Likha - Website Builder.",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en">
      <head>
        {process.env.NODE_ENV === "production" ? (
          <meta httpEquiv="Content-Security-Policy" content={productionCsp} />
        ) : null}
      </head>
      <body>{children}</body>
    </html>
  );
}
