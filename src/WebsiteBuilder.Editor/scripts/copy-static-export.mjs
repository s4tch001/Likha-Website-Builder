import { cp, mkdir, rm } from "node:fs/promises";
import { basename, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const editorRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const source = resolve(editorRoot, "out");
const target = resolve(editorRoot, "../WebsiteBuilder.App/wwwroot");

if (basename(target) !== "wwwroot" || basename(dirname(target)) !== "WebsiteBuilder.App") {
  throw new Error(`Refusing to replace unexpected export target: ${target}`);
}

await rm(target, { recursive: true, force: true });
await mkdir(target, { recursive: true });
await cp(source, target, { recursive: true });

console.log(`Copied Next.js static export to ${target}`);
