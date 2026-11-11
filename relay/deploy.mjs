// Copies the Unity WebGL build into ./public and deploys the worker.
// Usage: node deploy.mjs [--dry]
import { cpSync, rmSync, existsSync, readdirSync, statSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { join } from "node:path";

const build = join("..", "WebBuild");
if (!existsSync(join(build, "index.html"))) {
  console.error("No build found in " + build + " - build the game in Unity first (Our First Game > Build WebGL).");
  process.exit(1);
}
rmSync("public", { recursive: true, force: true });
cpSync(build, "public", { recursive: true });

// Cloudflare static hosting accepts files up to 25 MiB
let tooBig = false;
const walk = (d) => {
  for (const f of readdirSync(d)) {
    const p = join(d, f);
    const s = statSync(p);
    if (s.isDirectory()) walk(p);
    else if (s.size > 25 * 1024 * 1024) { console.error("Too big for Cloudflare (>25 MiB): " + p); tooBig = true; }
  }
};
walk("public");
if (tooBig) process.exit(1);

if (process.argv.includes("--dry")) {
  console.log("Copied the build to ./public (dry run).");
  process.exit(0);
}
const r = spawnSync("npx", ["--yes", "wrangler@latest", "deploy"], { stdio: "inherit", shell: true });
process.exit(r.status ?? 1);
