// Publishes the game's web files to Vercel (https://dohaandmustafafirstgame.vercel.app).
// The online relay stays on Cloudflare (Vercel cannot hold WebSocket connections), so the page is told
// where the relay lives. Run `node deploy.mjs` once first if the relay itself changed.
//
// Usage:  node deploy-vercel.mjs        (needs `npx vercel login` once)
import { cpSync, rmSync, existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { join } from "node:path";

const RELAY = "wss://ourfirstgame.doha-mustafa.workers.dev";
const PROJECT = "turbo-pavo-70q4dzn";                 // Vercel project behind dohaandmustafafirstgame.vercel.app
const SCOPE = "mustafa-aboalazm-s-projects";

const build = join("..", "WebBuild");
const site = join("..", "vercel-site");                // generated, ignored by git

if (!existsSync(join(build, "index.html"))) {
  console.error("No build found in " + build + " - build the game in Unity first (Our First Game > Build WebGL).");
  process.exit(1);
}

// keep the .vercel link between runs, replace everything else
mkdirSync(site, { recursive: true });
for (const f of ["index.html", "sw.js", "manifest.webmanifest", "Build", "icons"]) rmSync(join(site, f), { recursive: true, force: true });
cpSync(build, site, { recursive: true });

const indexPath = join(site, "index.html");
const html = readFileSync(indexPath, "utf8").replace('window.OFG_RELAY = "";', `window.OFG_RELAY = "${RELAY}";`);
if (!html.includes(RELAY)) { console.error("Could not set the relay address in index.html"); process.exit(1); }
writeFileSync(indexPath, html);

const run = (args) => spawnSync("npx", ["--yes", "vercel@latest", ...args], { cwd: site, stdio: "inherit", shell: true });
if (!existsSync(join(site, ".vercel", "project.json"))) {
  if (run(["link", "--yes", "--project", PROJECT, "--scope", SCOPE]).status !== 0) process.exit(1);
  rmSync(join(site, ".env.local"), { force: true });
}
process.exit(run(["deploy", "--prod", "--yes"]).status ?? 1);
