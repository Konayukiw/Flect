import { STRINGS } from "./strings.js";

const SAMPLE_SIZES = [12, 16, 20, 28, 40, 64];
const MARKDOWN_IMAGE_BYTES = 16 * 1024 * 1024;
const MARKDOWN_IMAGE_COUNT = 64;
const LANGUAGE_WORKERS = new Set(["JSON", "CSS", "SCSS", "LESS", "HTML", "Handlebars", "Razor", "TypeScript", "JavaScript"]);
const ENCODINGS = ["UTF-8", "UTF-16LE", "UTF-16BE", "Shift_JIS", "EUC-JP", "ISO-2022-JP", "GB18030", "Big5", "EUC-KR", "Windows-1252"];

const toolbar = document.getElementById("toolbar");
const banners = document.getElementById("banners");
const view = document.getElementById("view");

let session = null;
let editor = null;
let generation = 0;
let monacoReady = null;
let textBytes = null;
let textEncoding = null;
let wordWrap = false;
let onConverted = null;
let onConvertFailed = null;
const pending = new Map();

const webview = window.chrome.webview;

function t(key, ...values) {
  const table = STRINGS[session.locale] ?? STRINGS.en;
  const text = table[key] ?? STRINGS.en[key] ?? key;
  return values.reduce((result, value, index) => result.replace(`{${index}}`, value), text);
}

function node(tag, properties = {}, ...children) {
  const element = document.createElement(tag);
  Object.assign(element, properties);
  element.append(...children);
  return element;
}

function button(label, onClick, pressed) {
  const element = node("button", { type: "button", textContent: label });
  element.addEventListener("click", onClick);
  if (pressed !== undefined) element.setAttribute("aria-pressed", String(pressed));
  return element;
}

function command(name) {
  return new Promise((resolve) => {
    pending.set(name, resolve);
    webview.postMessage(name);
  });
}

webview.addEventListener("message", (event) => {
  const message = event.data;
  if (message.type === "ack") {
    pending.get(message.command)?.();
    pending.delete(message.command);
  } else if (message.type === "converted") {
    onConverted?.(message.url);
  } else if (message.type === "convert-failed") {
    onConvertFailed?.();
  }
});

function addBanner(text, danger) {
  banners.append(node("div", { className: danger ? "banner danger" : "banner", textContent: text }));
}

function removeEditor() {
  if (!editor) return;
  editor.getModel()?.dispose();
  editor.dispose();
  editor = null;
}

function clearView() {
  generation += 1;
  removeEditor();
  view.replaceChildren();
  return generation;
}

function showMessage(text) {
  clearView();
  view.append(node("div", { className: "center", textContent: text }));
}

function setToolbar(...items) {
  toolbar.replaceChildren(...items.filter(Boolean));
  toolbar.hidden = toolbar.childElementCount === 0;
}

function declaredName() {
  return session.extension.replace(".", "").toUpperCase();
}

function detectedName() {
  const key = `name.${session.mismatch}`;
  const table = STRINGS[session.locale] ?? STRINGS.en;
  return table[key] ?? STRINGS.en[key] ?? session.mismatch;
}


async function loadBytes() {
  if (!textBytes) {
    const response = await fetch("/api/text");
    textBytes = new Uint8Array(await response.arrayBuffer());
  }
  return textBytes;
}

function detectEncoding(bytes) {
  if (bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) return "UTF-8";
  if (bytes[0] === 0xff && bytes[1] === 0xfe) return "UTF-16LE";
  if (bytes[0] === 0xfe && bytes[1] === 0xff) return "UTF-16BE";
  try {
    new TextDecoder("UTF-8", { fatal: true }).decode(bytes, { stream: true });
    return "UTF-8";
  } catch {
    return session.fallbackEncoding;

  }
}

function decode(bytes, encoding) {
  return new TextDecoder(encoding).decode(bytes);
}

function loadMonaco() {
  if (monacoReady) return monacoReady;
  monacoReady = new Promise((resolve, reject) => {
    window.require.config({ paths: { vs: "/lib/monaco/vs" } });
    window.require(
      ["vs/editor/editor.main"],
      () => {
        const standard = window.MonacoEnvironment.getWorker;
        window.MonacoEnvironment = {
          getWorker(module, label) {
            if (LANGUAGE_WORKERS.has(label)) return new Worker(URL.createObjectURL(new Blob([""], { type: "text/javascript" })));
            return standard(module, label);
          },
        };
        resolve(window.monaco);
      },
      reject,
    );
  });
  return monacoReady;
}

function fontStack() {
  const family = session.fontFamily.replaceAll('"', "");
  return `"${family}", "HackGen Console", Consolas, monospace`;
}

function wrapMode() {
  return wordWrap ? "on" : "off";
}

async function renderEditor(mine) {
  const monaco = await loadMonaco();
  await document.fonts.load(`${session.fontSize}px "HackGen Console"`);
  if (mine !== generation) return;
  removeEditor();
  view.replaceChildren();
  const host = node("div", { className: "fill" });
  view.append(host);
  editor = monaco.editor.create(host, {
    value: decode(textBytes, textEncoding),
    language: session.language || "plaintext",
    theme: session.theme === "dark" ? "vs-dark" : "vs",
    readOnly: true,
    domReadOnly: true,
    automaticLayout: true,
    fontFamily: fontStack(),
    fontSize: session.fontSize,
    wordWrap: wrapMode(),
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    renderControlCharacters: true,
    unicodeHighlight: { ambiguousCharacters: true, invisibleCharacters: true, nonBasicASCII: false },
    links: false,
    hover: { enabled: false },
    occurrencesHighlight: "off",
    selectionHighlight: false,
    quickSuggestions: false,
    parameterHints: { enabled: false },
    suggestOnTriggerCharacters: false,
    lineNumbersMinChars: 3,
    glyphMargin: false,
    stickyScroll: { enabled: false },
  });
}

function encodingSelect() {
  const select = node("select", { title: "Encoding" });
  const names = ENCODINGS.includes(textEncoding) ? ENCODINGS : [textEncoding, ...ENCODINGS];
  for (const name of names) select.append(node("option", { value: name, textContent: name, selected: name === textEncoding }));
  select.addEventListener("change", () => {
    textEncoding = select.value;
    editor?.getModel()?.setValue(decode(textBytes, textEncoding));
  });
  return select;
}

async function showSource(extras = []) {
  const mine = clearView();
  if (session.binary) {
    setToolbar(...extras);
    showMessage(t("binary"));
    return;
  }
  const bytes = await loadBytes();
  if (mine !== generation) return;
  if (bytes.length === 0) {
    setToolbar(...extras);
    showMessage(t("empty"));
    return;
  }
  textEncoding ??= detectEncoding(bytes);
  if (session.truncated && !banners.querySelector(".truncated")) {
    banners.append(node("div", { className: "banner truncated", textContent: t("truncated") }));
  }
  setToolbar(...extras, node("div", { className: "spacer" }), encodingSelect());
  await renderEditor(mine);
}

function modeSwitch(current, select) {
  return [button(t("source"), () => select("source"), current === "source"), button(t("preview"), () => select("preview"), current === "preview")];
}

function withModes(initial, renderPreview, extraButtons) {
  const select = async (mode) => {
    const modes = modeSwitch(mode, select);
    if (mode === "source") {
      await showSource(modes);
      return;
    }
    const mine = clearView();
    setToolbar(...modes, ...(extraButtons?.() ?? []));
    await renderPreview(mine);
  };
  return select(initial);
}

async function allowRelative() {
  if (session.trust === "restricted" || session.relative) return;
  await command("allow-relative");
  session.relative = true;
}

function documentFrame(sandbox) {
  const frame = node("iframe", { className: "document" });
  frame.setAttribute("sandbox", sandbox);
  frame.setAttribute("referrerpolicy", "no-referrer");
  frame.setAttribute("allow", "");
  return frame;
}

function showText() {
  return showSource();
}

function showHtml() {
  let select = null;

  const render = async (mine) => {
    await allowRelative();
    if (mine !== generation) return;
    const frame = documentFrame(session.scripts ? "allow-scripts" : "");
    frame.src = session.docUrl;
    view.append(frame);
  };

  const extras = () => {
    const items = [];
    if (session.canAllowScripts && !session.scripts) {
      items.push(
        button(t("scripts"), async () => {
          await command("allow-scripts");
          session.scripts = true;
          session.relative = true;
          select("preview");
        }),
      );
    }
    if (session.canAllowNetwork && !session.network) {
      items.push(button(t("network"), () => webview.postMessage("allow-network")));
    }
    return items;
  };

  select = async (mode) => {
    const modes = modeSwitch(mode, select);
    if (mode === "source") {
      await showSource(modes);
      return;
    }
    const mine = clearView();
    setToolbar(...modes, ...extras());
    await render(mine);
  };

  return select(session.resume || session.trust === "trusted" ? "preview" : "source");
}

const MARKDOWN_STYLE = `
  :root { color-scheme: VAR_SCHEME; }
  body { margin: 0; padding: 14px 18px; background: VAR_BACKGROUND; color: VAR_TEXT; font: 14px/1.6 "Segoe UI Variable Text", "Segoe UI", "Yu Gothic UI", "Microsoft YaHei UI", sans-serif; overflow-wrap: anywhere; }
  h1, h2 { border-bottom: 1px solid VAR_BORDER; padding-bottom: .25em; }
  a { color: VAR_ACCENT; }
  img { max-width: 100%; }
  code, pre { font-family: VAR_CODE; font-size: .92em; }
  code { background: VAR_SURFACE; padding: .1em .3em; border-radius: 3px; }
  pre { background: VAR_SURFACE; padding: 10px 12px; border-radius: 4px; overflow: auto; }
  pre code { background: none; padding: 0; }
  blockquote { margin: 0; padding: 0 1em; border-left: 3px solid VAR_BORDER; color: VAR_MUTED; }
  table { border-collapse: collapse; }
  th, td { border: 1px solid VAR_BORDER; padding: 4px 10px; }
`;

function markdownStyle() {
  const styles = getComputedStyle(document.documentElement);
  const read = (name) => styles.getPropertyValue(name).trim();
  return MARKDOWN_STYLE.replaceAll("VAR_SCHEME", session.theme === "dark" ? "dark" : "light")
    .replaceAll("VAR_BACKGROUND", read("--background"))
    .replaceAll("VAR_TEXT", read("--text"))
    .replaceAll("VAR_BORDER", read("--border"))
    .replaceAll("VAR_ACCENT", read("--accent"))
    .replaceAll("VAR_SURFACE", read("--surface"))
    .replaceAll("VAR_MUTED", read("--muted"))
    .replaceAll("VAR_CODE", fontStack().replaceAll('"', "'"));
}

function readAsDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => resolve(reader.result));
    reader.addEventListener("error", reject);
    reader.readAsDataURL(blob);
  });
}

async function embedImage(image) {
  const source = image.attrGet("src") ?? "";
  if (/^[a-z][a-z0-9+.-]*:/i.test(source)) return;
  try {
    const response = await fetch(new URL(source, session.docBase).href);
    if (!response.ok) return;
    const blob = await response.blob();
    if (blob.size <= MARKDOWN_IMAGE_BYTES && blob.type.startsWith("image/")) image.attrSet("src", await readAsDataUrl(blob));
  } catch {
    image.attrSet("src", "");
  }
}

async function embedImages(tokens) {
  if (!session.relative) return;
  const images = tokens.flatMap((token) => (token.children ?? []).filter((child) => child.type === "image"));
  await Promise.all(images.slice(0, MARKDOWN_IMAGE_COUNT).map(embedImage));
}

function showMarkdown() {
  if (session.binary) return showSource();
  return withModes("preview", async (mine) => {
    await allowRelative();
    const { default: markdownit } = await import("/lib/markdown-it/markdown-it.min.mjs");
    const bytes = await loadBytes();
    if (mine !== generation) return;
    textEncoding ??= detectEncoding(bytes);
    const parser = markdownit({ html: false, linkify: false });
    const environment = {};
    const tokens = parser.parse(decode(bytes, textEncoding), environment);
    await embedImages(tokens);
    if (mine !== generation) return;
    const body = parser.renderer.render(tokens, parser.options, environment);
    const policy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
    const frame = documentFrame("");
    frame.srcdoc = `<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="${policy}"><style>${markdownStyle()}</style></head><body>${body}</body></html>`;
    view.append(frame);
  });
}

function showSvg() {
  if (session.binary) return showSource();
  return withModes("preview", async () => {
    view.append(node("div", { className: "picture" }, node("img", { src: session.docUrl, alt: session.name })));
  });
}

function showPdf() {
  setToolbar();
  const frame = documentFrame("allow-scripts");
  const parameters = new URLSearchParams({ file: session.docUrl, theme: session.theme, locale: session.locale });
  frame.src = `${session.viewerBase}viewer/pdf.html#${parameters}`;
  view.append(frame);
}

async function showFont() {
  setToolbar();
  const family = "FlectPreviewSample";
  try {
    const face = new FontFace(family, `url("${session.docUrl}")`);
    await face.load();
    document.fonts.add(face);
  } catch {
    showMessage(t("fontFailed"));
    return;
  }
  const sample = session.sample || t("sample");
  const rows = SAMPLE_SIZES.map((size) => {
    const text = node("div", { className: "sample", textContent: sample });
    text.style.fontFamily = `"${family}"`;
    text.style.fontSize = `${size}px`;
    text.style.lineHeight = "1.25";
    return node("div", { className: "row" }, node("div", { className: "size", textContent: String(size) }), text);
  });
  view.append(node("div", { className: "specimen" }, node("div", { className: "title", textContent: session.name }), ...rows));
}

function showMedia() {
  setToolbar();
  const isAudio = session.kind === "audio";
  const player = node(isAudio ? "audio" : "video", { controls: true, preload: "metadata" });
  player.setAttribute("controlslist", "nodownload noremoteplayback");
  player.setAttribute("disablepictureinpicture", "");
  const holder = node("div", { className: isAudio ? "player audio" : "player" }, player);
  view.append(holder);

  const message = node("div", { className: "message" });

  const beginConversion = () => {
    message.textContent = t("converting");
    message.replaceChildren(t("converting"));
    webview.postMessage("convert");
  };

  const offerConversion = () => {
    if (session.trust === "restricted" || session.marked) {
      message.replaceChildren(t("cannotPlay"), node("br"), node("br"), t("convertRestricted"));
      holder.replaceChildren(message);
      return;
    }
    if (!session.canConvert) {
      holder.replaceChildren(node("div", { className: "message", textContent: t("cannotConvert") }));
      return;
    }
    const start = button(t("convert"), beginConversion);
    start.className = "primary";
    message.replaceChildren(t("cannotPlay"), node("br"), node("br"), start);
    holder.replaceChildren(message);
  };

  onConverted = (url) => {
    player.addEventListener("error", () => holder.replaceChildren(node("div", { className: "message", textContent: t("convertFailed") })), { once: true });
    player.src = url;
    holder.replaceChildren(player);
  };

  onConvertFailed = () => {
    message.replaceChildren(t("convertFailed"));
  };

  player.addEventListener("error", offerConversion, { once: true });
  player.src = session.docUrl;
}

const VIEWS = {
  text: showText,
  html: showHtml,
  markdown: showMarkdown,
  svg: showSvg,
  pdf: showPdf,
  font: showFont,
  video: showMedia,
  audio: showMedia,
};

async function main() {
  session = await (await fetch("/api/session")).json();
  document.documentElement.dataset.theme = session.theme;
  document.documentElement.lang = session.locale;
  wordWrap = session.wordWrap;

  if (session.mismatch) addBanner(t("mismatch", declaredName(), detectedName()), true);
  if (session.trust === "restricted") addBanner(t("restricted"));

  const show = VIEWS[session.kind];
  if (show) await show();
  else showMessage(t("unsupported"));
}

main().catch((error) => {
  view.replaceChildren(node("div", { className: "center", textContent: String(error) }));
});
