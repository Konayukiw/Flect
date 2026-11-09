import * as pdfjs from "../lib/pdfjs/pdf.min.mjs";

const STRINGS = {
  en: { password: "This PDF is password protected and unable to be previewed.", failed: "This PDF could not be displayed." },
  ja: { password: "このPDFはパスワードで保護されているため、プレビューできません。", failed: "PDFの表示に失敗しました。" },
  zh: { password: "此 PDF 受密码保护，无法预览。", failed: "无法显示此 PDF。" },
};

const parameters = new URLSearchParams(location.hash.slice(1));
const file = parameters.get("file");
const locale = parameters.get("locale") ?? "en";
const library = new URL("../lib/pdfjs/", location.href).href;

document.documentElement.dataset.theme = parameters.get("theme") ?? "light";
document.documentElement.lang = locale;

const pages = document.getElementById("pages");
const strings = STRINGS[locale] ?? STRINGS.en;
const margin = 16;
const maximumScale = 4;

let documentProxy = null;
let basePage = null;
const holders = [];
const active = new Map();

function showNotice(text) {
  pages.replaceChildren();
  const notice = document.createElement("div");
  notice.className = "notice";
  notice.textContent = text;
  pages.append(notice);
}

function fitScale(width) {
  return Math.min((Math.max(document.documentElement.clientWidth, 120) - margin) / width, maximumScale);
}

function sizeHolder(holder, width, height) {
  holder.style.width = `${Math.floor(width)}px`;
  holder.style.height = `${Math.floor(height)}px`;
}

function release(holder) {
  const entry = active.get(holder);
  if (!entry) return;
  entry.task?.cancel();
  entry.text?.cancel();
  active.delete(holder);
  holder.replaceChildren();
}

async function render(holder) {
  if (active.has(holder)) return;
  const entry = {};
  active.set(holder, entry);
  try {
    const page = await documentProxy.getPage(Number(holder.dataset.page));
    if (active.get(holder) !== entry) return;

    const scale = fitScale(page.getViewport({ scale: 1 }).width);
    const viewport = page.getViewport({ scale });
    sizeHolder(holder, viewport.width, viewport.height);

    const ratio = window.devicePixelRatio || 1;
    const canvas = document.createElement("canvas");
    canvas.width = Math.floor(viewport.width * ratio);
    canvas.height = Math.floor(viewport.height * ratio);

    const layer = document.createElement("div");
    layer.className = "textLayer";
    layer.style.setProperty("--total-scale-factor", String(scale));
    holder.replaceChildren(canvas, layer);

    entry.task = page.render({ canvas, viewport, transform: [ratio, 0, 0, ratio, 0, 0] });
    await entry.task.promise;
    if (active.get(holder) !== entry) return;

    entry.text = new pdfjs.TextLayer({ textContentSource: page.streamTextContent(), container: layer, viewport });
    await entry.text.render();
  } catch (error) {
    if (error?.name !== "RenderingCancelledException" && error?.name !== "AbortException") console.error(error);
  }
}

const observer = new IntersectionObserver(
  (entries) => {
    for (const entry of entries) {
      if (entry.isIntersecting) render(entry.target);
      else release(entry.target);
    }
  },
  { rootMargin: "1200px 0px" },
);

function layout() {
  const viewport = basePage.getViewport({ scale: 1 });
  const scale = fitScale(viewport.width);
  for (const holder of holders) {
    release(holder);
    sizeHolder(holder, viewport.width * scale, viewport.height * scale);
    observer.unobserve(holder);
    observer.observe(holder);
  }
}

async function start() {
  globalThis.pdfjsWorker = await import("../lib/pdfjs/pdf.worker.min.mjs");

  documentProxy = await pdfjs.getDocument({
    url: file,
    isEvalSupported: false,
    enableXfa: false,
    useSystemFonts: false,
    cMapUrl: `${library}cmaps/`,
    cMapPacked: true,
    standardFontDataUrl: `${library}standard_fonts/`,
    wasmUrl: `${library}wasm/`,
  }).promise;

  basePage = await documentProxy.getPage(1);
  for (let index = 1; index <= documentProxy.numPages; index += 1) {
    const holder = document.createElement("div");
    holder.className = "page";
    holder.dataset.page = String(index);
    holders.push(holder);
    pages.append(holder);
  }
  layout();

  let timer = 0;
  window.addEventListener("resize", () => {
    clearTimeout(timer);
    timer = setTimeout(layout, 150);
  });
}

start().catch((error) => {
  showNotice(error?.name === "PasswordException" ? strings.password : `${strings.failed} ${error?.message ?? ""}`);
});
