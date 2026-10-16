import { createServer } from "node:http";
import { createHash } from "node:crypto";
import { readFileSync, appendFileSync } from "node:fs";
import { basename } from "node:path";
import { parseArgs } from "node:util";

const { values: options } = parseArgs({
  options: {
    script: { type: "string" },
    setup: { type: "string" },
    tag: { type: "string", default: "v1.0.0" },
    older: { type: "string", default: "v0.0.1" },
    mode: { type: "string", default: "normal" },
    log: { type: "string" },
    replace: { type: "string", multiple: true, default: [] },
  },
});

const setup = readFileSync(options.setup);
const assetName = basename(options.setup);
const digest = options.mode === "bad-digest" ? "0".repeat(64) : createHash("sha256").update(setup).digest("hex");

const server = createServer((request, response) => {
  const url = new URL(request.url, "http://127.0.0.1");
  if (options.log) appendFileSync(options.log, `${request.method} ${url.pathname}${url.search}\n`);
  const origin = `http://127.0.0.1:${server.address().port}`;

  const release = (tag) => ({
    tag_name: tag,
    draft: false,
    prerelease: false,
    assets: [
      { name: "Source.zip", size: 10, digest: `sha256:${"1".repeat(64)}`, browser_download_url: `${origin}/download/${tag}/Source.zip` },
      { name: assetName, size: setup.length, digest: `sha256:${digest}`, browser_download_url: `${origin}/download/${tag}/${assetName}` },
    ],
  });

  const json = (status, body) => {
    response.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
    response.end(JSON.stringify(body));
  };

  if (url.pathname === "/install.ps1") {
    let text = readFileSync(options.script, "utf8").replaceAll("https://api.github.com", origin);
    for (const pair of options.replace) {
      const [from, to] = pair.split("=>");
      text = text.replaceAll(from, to);
    }
    response.writeHead(200, { "Content-Type": "application/octet-stream" });
    response.end(text);
    return;
  }

  if (/^\/repos\/[^/]+\/[^/]+\/releases\/latest$/.test(url.pathname)) {
    if (options.mode === "no-release") return json(404, { message: "Not Found" });
    return json(200, release(options.tag));
  }

  if (/^\/repos\/[^/]+\/[^/]+\/releases$/.test(url.pathname)) {
    if (options.mode === "no-release") return json(200, []);
    const page = Number(url.searchParams.get("page") ?? "1");
    return json(200, page === 1 ? [release(options.tag), release(options.older)] : []);
  }

  const download = url.pathname.match(/^\/download\/([^/]+)\/(.+)$/);
  if (download && download[2] === assetName) {
    response.writeHead(200, { "Content-Type": "application/octet-stream", "Content-Length": setup.length });
    response.end(setup);
    return;
  }

  json(404, { message: "Not Found" });
});

server.listen(0, "127.0.0.1", () => console.log(`listening ${server.address().port}`));
