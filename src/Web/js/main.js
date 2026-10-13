let anime = null;
try {
  const m = await import("animejs");
  anime = m.default || m;
} catch { anime = null; }

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));

const icons = {
  resize: '<svg viewBox="0 0 24 24"><path d="M4 4h7v7H4zM13 13h7v7h-7z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M9 9l11-5-5 11" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  compress: '<svg viewBox="0 0 24 24"><path d="M12 4v16M7 9l5-5 5 5M7 15l5 5 5-5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  rotate: '<svg viewBox="0 0 24 24"><path d="M20 11a8 8 0 1 0-2.3 5.7M20 5v6h-6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  convert: '<svg viewBox="0 0 24 24"><path d="M4 7h12M13 4l3 3-3 3M20 17H8M11 14l-3 3 3 3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  ocr: '<svg viewBox="0 0 24 24"><path d="M4 6V4h16v2M4 18v2h16v-2M7 9v6H9.4l2.1-4.6h.3L13.8 15H16V9h-1.5v3.9L13 9h-1.8L9.7 12.9V9z" fill="currentColor"/></svg>',
  trim: '<svg viewBox="0 0 24 24"><path d="M5 6l14 12M12 3v4M12 17v4M6 4l4 4M14 16l4 4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
  thumb: '<svg viewBox="0 0 24 24"><rect x="4" y="5" width="16" height="14" rx="2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M4 17l5-5 3 3 2-2 6 6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><circle cx="9" cy="10" r="1.6" fill="currentColor"/></svg>',
  audio: '<svg viewBox="0 0 24 24"><path d="M8 5v13a3 3 0 1 1-2-2.83M9.5 5L21 2v15a3 3 0 1 1-2-2.83z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
  dup: '<svg viewBox="0 0 24 24"><path d="M9 8h9a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M6 16H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h9a1 1 0 0 1 1 1v1" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
  tree: '<svg viewBox="0 0 24 24"><path d="M6 3v18M10 7h5v3M10 13h8v3M6 7h2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
  rename: '<svg viewBox="0 0 24 24"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round"/></svg>',
  analyze: '<svg viewBox="0 0 24 24"><path d="M4 20V10M10 20V4M16 20v-7M22 20H2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
  preview: '<svg viewBox="0 0 24 24"><path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6z" fill="none" stroke="currentColor" stroke-width="1.7"/><circle cx="12" cy="12" r="2.6" fill="none" stroke="currentColor" stroke-width="1.7"/></svg>',
  merge: '<svg viewBox="0 0 24 24"><path d="M12 3v18M3 12l3-3M3 12l3 3M21 12l-3-3M21 12l-3 3M12 7l-3 3M12 7l3 3M12 17l-3-3M12 17l3-3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
  sort: '<svg viewBox="0 0 24 24"><path d="M8 7l-3 3m0 0L2 7m3 3V3M13 5h9M13 12h6M13 19h3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  bombs: '<svg viewBox="0 0 24 24"><path d="M17 10l1.5 1.5L20 10l1.5 1.5L23 10a8 8 0 1 1-10 10l1.5-1.5L13 17l1.5-1.5L13 14l1.5-1.5z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  extract: '<svg viewBox="0 0 24 24"><path d="M12 3v12M7 10l5 5 5-5M4 20h16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
};

const MENU = {
  image: [
    { k: "menu.resize", s: "resize" },
    { k: "menu.compress", s: "compress" },
    { k: "menu.rotate", s: "rotate" },
    { k: "menu.convert", s: "convert", sub: true },
    { k: "menu.ocr", s: "ocr" },
  ],
  imageSub: [
    { fmt: "PNG" }, { fmt: "JPG" }, { fmt: "WEBP" }, { fmt: "HEIC" }, { fmt: "ICO" }, { fmt: "GIF" }, { fmt: "SVG" },
  ],
  video: [
    { k: "menu.resize", s: "resize" },
    { k: "menu.trim", s: "trim" },
    { k: "menu.thumb", s: "thumb" },
    { k: "menu.compress", s: "compress" },
    { k: "menu.rotate", s: "rotate" },
    { k: "menu.convert", s: "convert", sub: true },
    { k: "menu.audio", s: "audio" },
  ],
  videoSub: [
    { fmt: "MP4" }, { fmt: "MOV" }, { fmt: "MKV" }, { fmt: "M4A" }, { fmt: "AVI" }, { fmt: "WEBM" }, { fmt: "FLV" }, { fmt: "GIF" },
  ],
  audio: [
    { k: "menu.convert", s: "convert" },
  ],
  pdf: [
    { k: "menu.preview", s: "preview" },
    { k: "menu.merge", s: "merge" },
    { k: "menu.convert", s: "convert", sub: true },
  ],
  pdfSub: [
    { fmt: "PNG" }, { fmt: "JPG" }, { fmt: "TXT" },
  ],
  folder: [
    { k: "menu.dup", s: "dup" },
    { k: "menu.empty", s: "dup" },
    { k: "menu.tree", s: "tree" },
    { k: "menu.rename", s: "rename" },
    { k: "menu.analyze", s: "analyze" },
    { k: "menu.compress", s: "compress" },
  ],
  archive: [
    { k: "menu.extract", s: "extract" },
  ],
  text: [
    { k: "menu.beautify", s: "sort" },
    { k: "menu.sortKeys", s: "sort" },
  ],
};

const TASKS = {
  "menu.resize (image)": { scans: [["scan.analyzingImage", "45%"], ["scan.resizing", "78%"]], job: "resize" },
  "menu.compress (image)": { scans: [["scan.analyzingImage", "40%"], ["scan.compress", "72%"]], job: "compress" },
  "menu.rotate (image)": { scans: [["scan.rotating", "80%"]], job: "compress" },
  "menu.ocr (image)": { scans: [["scan.ocr", "55%"]], job: "ocr" },
  "menu.resize (video)": { scans: [["scan.analyzingVideo", "30%"], ["scan.resizing", "75%"]], job: "compress" },
  "menu.trim (video)": { scans: [["scan.trimming", "95%"]], job: "compress" },
  "menu.thumb (video)": { scans: [["scan.exportFrame", "80%"]], job: "convert" },
  "menu.compress (video)": { scans: [["scan.analyzingVideo", "25%"], ["scan.compress", "70%"]], job: "compress" },
  "menu.rotate (video)": { scans: [["scan.rotating", "60%"]], job: "compress" },
  "menu.audio (video)": { scans: [["scan.extractAudio", "66%"], ["scan.writing", "88%"]], job: "compress" },
  "menu.convert (image)": { scans: [["scan.writing", "64%"]], job: "convert" },
  "menu.convert (video)": { scans: [["scan.converting", "50%"]], job: "convert" },
  "menu.convert (audio)": { scans: [["scan.converting", "70%"]], job: "convert" },
  "menu.preview (pdf)": { scans: [["scan.preview", "40%"]], job: "convert" },
  "menu.merge (pdf)": { scans: [["scan.merging", "75%"]], job: "convert" },
  "menu.convert (pdf)": { scans: [["scan.writing", "82%"]], job: "convert" },
  "menu.dup (folder)": { scans: [["scan.scanFolder", "35%"], ["scan.compareHash", "70%"], ["scan.moveBin", "92%"]], job: "duplicate" },
  "menu.empty (folder)": { scans: [["scan.scanning", "55%"], ["scan.moveBin", "90%"]], job: "duplicate" },
  "menu.tree (folder)": { scans: [["scan.tree", "80%"]], job: "convert" },
  "menu.rename (folder)": { scans: [["scan.renaming", "76%"]], job: "convert" },
  "menu.analyze (folder)": { scans: [["scan.analyzing", "50%"], ["scan.report", "88%"]], job: "duplicate" },
  "menu.compress (folder)": { scans: [["scan.compressFolder", "70%"]], job: "compress" },
  "menu.extract (archive)": { scans: [["scan.extracting", "65%"], ["scan.writingFiles", "88%"]], job: "convert" },
};

const RESULTS = {
  duplicate: { cls: "ic-ok", bar: "60%", titleKey: "result.done", descKey: "result.dup" },
  resize: { cls: "ic-ok", bar: "100%", titleKey: "result.done", descKey: "result.resize" },
  compress: { cls: "ic-ok", bar: "100%", titleKey: "result.done", descKey: "result.compress" },
  convert: { cls: "ic-ok", bar: "100%", titleKey: "result.done", descKey: "result.convert" },
  ocr: { cls: "ic-ok", bar: "100%", titleKey: "result.done", descKey: "result.ocr" },
};

const applyTheme = (th) => { document.documentElement.dataset.theme = th; };
try {
  const saved = localStorage.getItem("flect-theme");
  applyTheme(saved === "light" || saved === "dark" ? saved : "dark");
  $("#themeToggle").addEventListener("click", () => {
    const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
    applyTheme(next);
    try { localStorage.setItem("flect-theme", next); } catch { /* ignore */ }
  });
} catch {/* ignore */}

try {
  const { hostname, pathname, protocol } = location;
  if (protocol === "https:" && /\.github\.io$/i.test(hostname) && /^\/Flect\//i.test(pathname)) {
    const badge = $(".dev-badge");
    badge.textContent = "under development";
    badge.classList.add("show");
  }
} catch { /* ignore */ }

const cursor = $("#cursor");
const finePointer = matchMedia("(min-width: 1080px) and (pointer: fine)");
let cursorRaf = 0;
if (cursor) {
  addEventListener("pointermove", (e) => {
    if (!finePointer.matches) return;
    cancelAnimationFrame(cursorRaf);
    cursorRaf = requestAnimationFrame(() => {
      cursor.style.transform = `translate(${e.clientX - cursor.offsetWidth / 2}px, ${e.clientY - cursor.offsetHeight / 2}px)`;
    });
  }, { passive: true });
}

let lastY = 0;
addEventListener("scroll", () => {
  const head = $(".site-head");
  if (!head) return;
  const y = scrollY;
  head.classList.toggle("hidden", y > 520 && y > lastY);
  lastY = y;
}, { passive: true });

const revealEls = $$(".reveal");
if ("IntersectionObserver" in window) {
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
    }
  }, { threshold: 0.12 });
  revealEls.forEach((el) => io.observe(el));
} else {
  revealEls.forEach((el) => el.classList.add("in"));
}

const desk = $("#desk");
const iconsEl = $("#deskIcons");
let menuOpen = false;

const resetMenu = () => {
  const menu = $("#ctx");
  menu.classList.remove("open");
  menu.setAttribute("aria-hidden", "true");
  menuOpen = false;
};

const openMenuAt = (x, y) => {
  const menu = $("#ctx");
  const rect = menu.getBoundingClientRect();
  const border = 10;
  menu.style.left = clamp(x, border, innerWidth - rect.width - border) + "px";
  menu.style.top = clamp(y, border, innerHeight - rect.height - border) + "px";
  menu.classList.add("open");
  menu.setAttribute("aria-hidden", "false");
  menuOpen = true;
};

const itemSvg = (s) => `<span class="ct-ic">${icons[s] || icons.convert}</span>`;

const headerHtml = (name, kind) =>
  `<div class="ctx-h"><span class="ctx-f"></span><span class="n">${name}</span><span class="t">Flect</span></div>`;

const menuFor = (kind) =>
  kind === "image" ? MENU.image
  : kind === "video" ? MENU.video
  : kind === "audio" ? MENU.audio
  : kind === "pdf" ? MENU.pdf
  : kind === "folder" ? MENU.folder
  : kind === "archive" ? MENU.archive
  : MENU.text;

const subFor = (kind) =>
  kind === "image" ? MENU.imageSub
  : kind === "video" ? MENU.videoSub
  : kind === "pdf" ? MENU.pdfSub
  : [];

const runTask = async (kind, k) => {
  const tsk = TASKS[`${k} (${kind})`];
  if (tsk) {
    await runScan((tsk.scans || []).map(([l, p]) => [t(l), p]));
    await finishScan(true, tsk.job);
    return;
  }
  await runScan([[t("scan.applying", { label: t(k) }), "70%"]]);
  await finishScan(true, "convert");
};

const renderMenu = (name, kind) => {
  const body = $("#ctxBody");
  let html = headerHtml(name, kind);
  html += `<div class="ctx-sep"></div>`;
  for (const it of menuFor(kind)) {
    html += `<button class="ctx-item" type="button" data-k="${it.k}">${itemSvg(it.s)}`
      + `<span class="ct-lbl">${t(it.k)}</span>${it.sub ? '<span class="ct-sub">›</span>' : ""}</button>`;
  }
  html += `<div class="ctx-sep"></div>`;
  html += `<button class="ctx-item" type="button" data-k="ctx.custom">${itemSvg("sort")}<span class="ct-lbl">${t("ctx.custom")}</span></button>`;
  body.innerHTML = html;
  attachHandlers(body, name, kind, "main");
};

const renderSubMenu = (name, kind) => {
  const body = $("#ctxBody");
  const subs = subFor(kind);
  let html = headerHtml(name, kind);
  html += `<div class="ctx-sep"></div>`;
  html += `<button class="ctx-item" type="button" data-back="1">${itemSvg("resize")}<span class="ct-lbl">${t("ctx.back")}</span></button>`;
  html += `<div class="ctx-sep"></div>`;
  for (const f of subs) {
    html += `<button class="ctx-item" type="button" data-to="${f.fmt}"><span class="ct-ic">${icons.convert}</span><span class="ct-lbl">${t("menu.to", { fmt: f.fmt })}</span></button>`;
  }
  body.innerHTML = html;

  for (const btn of $$(".ctx-item", body)) {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const to = btn.dataset.to;
      const back = btn.dataset.back;
      if (back) { renderMenu(name, kind); return; }
      resetMenu();
      runScan([[t("scan.convertTo", { fmt: to }), "60%"]])
        .then(() => finishScan(true, "convert"));
    });
  }
};

const attachHandlers = (body, name, kind, mode) => {
  for (const btn of $$(".ctx-item", body)) {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const k = btn.dataset.k;
      if (!k) return;
      resetMenu();
      if (mode === "main" && k === "menu.convert" && subFor(kind).length) {
        renderSubMenu(name, kind);
        openMenuAt(parseFloat(menuX) || 320, parseFloat(menuY) || 320);
        return;
      }
      runTask(kind, k);
    });
  }
};

let menuX = null, menuY = null;

const kindOf = (icon) => {
  if (icon.dataset.kind === "folder") return "folder";
  const map = { mp4: "video", mov: "video", mkv: "video", avi: "video", webm: "video", flv: "video", m4a: "audio", mp3: "audio", wav: "audio", aac: "audio", ogg: "audio", wma: "audio", jpeg: "image", jpg: "image", png: "image", webp: "image", heic: "image", ico: "image", gif: "image", svg: "image", pdf: "pdf", zip: "archive", "7z": "archive", tar: "archive", gz: "archive", rar: "archive" };
  return map[(icon.dataset.ext || "").toLowerCase()] || "text";
};

iconsEl.addEventListener("contextmenu", (e) => {
  const icon = e.target.closest(".icon");
  if (!icon) return;
  e.preventDefault();
  resetMenu();
  $$(".icon").forEach((i) => i.classList.remove("selected"));
  icon.classList.add("selected");
  if (anime) {
    anime({ targets: icon.querySelector(".ic"), scale: [1, 0.92, 1], duration: 260, easing: "easeOutQuad" });
  }
  renderMenu(icon.dataset.name, kindOf(icon));
  menuX = e.clientX; menuY = e.clientY;
  openMenuAt(e.clientX, e.clientY);
});

iconsEl.addEventListener("click", (e) => {
  const icon = e.target.closest(".icon");
  if (!icon) return;
  $$(".icon").forEach((i) => i.classList.remove("selected"));
  icon.classList.add("selected");
});

iconsEl.addEventListener("dblclick", (e) => {
  const icon = e.target.closest(".icon");
  if (!icon || icon.dataset.kind !== "folder") return;
  resetMenu();

  const rect = icon.getBoundingClientRect();
  const wrap = document.createElement("div");
  wrap.className = "rename-bubble";
  const input = document.createElement("input");
  input.value = icon.dataset.name;
  input.spellcheck = false;
  const okBtn = document.createElement("button");
  okBtn.textContent = "✓";
  okBtn.title = t("rename.ok");
  const cancelBtn = document.createElement("button");
  cancelBtn.textContent = "✕";
  cancelBtn.title = t("rename.cancel");
  wrap.append(input, okBtn, cancelBtn);

  const wrapOff = () => { wrap.remove(); $("#renameBackdrop").classList.remove("show"); };
  const commit = () => {
    if (input.value.trim()) {
      icon.dataset.name = input.value.trim();
      const nm = icon.querySelector(".nm");
      nm.textContent = input.value.trim();
    }
    wrapOff();
  };

  okBtn.addEventListener("click", commit);
  cancelBtn.addEventListener("click", wrapOff);
  input.addEventListener("keydown", (ev) => {
    if (ev.key === "Enter") commit();
    if (ev.key === "Escape") wrapOff();
  });

  document.body.appendChild(wrap);
  $("#renameBackdrop").classList.add("show");
  wrap.style.left = clamp(rect.left, 8, document.body.offsetWidth - 260) + "px";
  wrap.style.top = clamp(rect.top - wrap.offsetHeight - 8, 8, innerHeight - wrap.offsetHeight - 8) + "px";
  input.focus();
  input.select();
});

$("#renameBackdrop").addEventListener("click", () => {
  $(".rename-bubble")?.remove();
  $("#renameBackdrop").classList.remove("show");
});

desk.addEventListener("contextmenu", (e) => {
  if (e.target.closest(".icon")) return;
  e.preventDefault();
  resetMenu();
  $$(".icon").forEach((i) => i.classList.remove("selected"));
  const body = $("#ctxBody");
  body.innerHTML = headerHtml(t("desk.title"), "desk")
    + '<div class="ctx-sep"></div>'
    + `<button class="ctx-item" type="button" data-row="1">${itemSvg("resize")}<span class="ct-lbl">${t("desk.settings")}</span></button>`
    + `<button class="ctx-item" type="button" data-row="2">${itemSvg("tree")}<span class="ct-lbl">${t("desk.about")}</span></button>`;
  for (const b of $$(".ctx-item", body)) {
    b.addEventListener("click", (e2) => {
      e2.stopPropagation();
      const row = b.dataset.row;
      resetMenu();
      if (row === "1") failScan();
      if (row === "2") openModal();
    });
  }
  menuX = e.clientX; menuY = e.clientY;
  openMenuAt(e.clientX, e.clientY);
});

addEventListener("pointerdown", (e) => {
  if ($("#ctx").contains(e.target) || e.target.closest(".scan-overlay") || e.target.closest(".rename-bubble")) return;
  if (!e.target.closest(".icon")) {
    resetMenu();
    $$(".icon").forEach((i) => i.classList.remove("selected"));
  }
});

addEventListener("keydown", (e) => {
  if (e.key === "Escape") {
    resetMenu();
    $(".rename-bubble")?.remove();
    $("#renameBackdrop")?.classList.remove("show");
  }
});

const stepOverlay = (label, pct) => {
  $("#scanOverlay").classList.add("open");
  $("#scanTitle").textContent = label;
  $("#scanBar").style.transition = "none";
  $("#scanBar").style.width = pct;
};

const runScan = (steps) => new Promise((resolve) => {
  if (!steps || !steps.length) { 
    setTimeout(resolve, 300);
    return;
  }
  stepOverlay(...steps.shift());
  const timer = setInterval(() => {
    const next = steps.shift();
    if (!next) { clearInterval(timer); setTimeout(resolve, 400); return; }
    stepOverlay(...next);
  }, 480);
});

const finishScan = (ok, job) => new Promise((resolve) => {
  const r = RESULTS[job] || RESULTS.convert;
  const icon = $("#scanOverlay .scan-icon");
  icon.className = "scan-icon " + (ok ? r.cls : "ic-fail");
  $("#scanTitle").textContent = ok ? t(r.titleKey) : t("result.failed");
  $("#scanDesc").textContent = ok ? t(r.descKey) : t("scan.failDesc");
  $("#scanBar").style.transition = "none";
  $("#scanBar").style.width = (ok ? r.bar : "0%");
  setTimeout(() => {
    const ov = $("#scanOverlay");
    ov.classList.remove("open");
    setTimeout(() => { icon.className = "scan-icon"; $("#scanTitle").textContent = t("scan.start"); $("#scanDesc").textContent = ""; resolve(); }, 380);
  }, 2100);
});

const failScan = () => {
  $("#scanOverlay").classList.add("open");
  $("#scanTitle").textContent = t("result.failed");
  $("#scanDesc").textContent = t("scan.failDemo");
  const icon = $("#scanOverlay .scan-icon");
  icon.className = "scan-icon ic-fail";
  $("#scanBar").style.transition = "none";
  $("#scanBar").style.width = "0%";
  setTimeout(() => {
    $("#scanOverlay").classList.remove("open");
    setTimeout(() => { icon.className = "scan-icon"; $("#scanTitle").textContent = t("scan.start"); $("#scanDesc").textContent = ""; }, 380);
  }, 2300);
};

let modalEl = null;
const openModal = () => {
  modalEl = document.createElement("div");
  modalEl.className = "modal-bg";
  modalEl.innerHTML = `
    <div class="modal">
      <div class="modal-head">
        <span class="modal-mark">F</span>
        <span class="modal-title">${t("modal.title")}</span>
        <button class="modal-x" type="button" aria-label="${t("modal.close")}">✕</button>
      </div>
      <div class="modal-body">
        <p>${t("modal.p1")}</p>
        <p>${t("modal.p2")}</p>
        <p class="modal-tip">${t("modal.tip")}</p>
      </div>
    </div>`;
  document.body.appendChild(modalEl);
  requestAnimationFrame(() => modalEl.classList.add("open"));
  modalEl.addEventListener("click", (e) => {
    if (e.target === modalEl || e.target.closest(".modal-x")) closeModal();
  });
  addEventListener("keydown", (e) => { if (e.key === "Escape") closeModal(); }, { once: true });
};

const closeModal = () => {
  if (!modalEl) return;
  modalEl.classList.remove("open");
  setTimeout(() => modalEl.remove(), 220);
  modalEl = null;
};

$("#year").textContent = new Date().getFullYear();

if (anime && finePointer) {
  const ring = document.querySelector(".brand-mark .mark-ring");
  const a = document.querySelector(".brand-mark .mark-a");
  if (a) {
    const tl = anime.timeline({ autoplay: false });
    if (ring) tl.add({ targets: ring, scale: [0, 1.25, 1], opacity: [0, 1, 0], duration: 1400, easing: "easeOutCubic" });
    tl.add({ targets: a, rotate: [0, 360], duration: 900, easing: "easeInOutBack" });
    tl.play();
  }
}

let confetti = null;
try {
  const cm = await import("canvas-confetti");
  confetti = cm.default || cm;
} catch { confetti = null; }

const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const introFresh = performance.now() < 3500;

const progressEl = $("#progress");
if (progressEl) {
  const setProgress = () => {
    const doc = document.documentElement;
    const max = doc.scrollHeight - innerHeight;
    progressEl.style.transform = `scaleX(${max > 0 ? scrollY / max : 0})`;
  };
  addEventListener("scroll", setProgress, { passive: true });
  addEventListener("resize", setProgress);
  setProgress();
}

const particlesEl = $("#particles");
if (particlesEl && !reducedMotion) {
  const pctx = particlesEl.getContext("2d");
  const hexRgb = (hex) => {
    const v = hex.replace("#", "");
    const n = v.length === 3 ? v.split("").map((c) => c + c).join("") : v;
    return [parseInt(n.slice(0, 2), 16), parseInt(n.slice(2, 4), 16), parseInt(n.slice(4, 6), 16)];
  };
  let cA = [138, 180, 255];
  let cB = [196, 181, 253];
  const readColors = () => {
    try {
      const cs = getComputedStyle(document.documentElement);
      cA = hexRgb(cs.getPropertyValue("--acc").trim());
      cB = hexRgb(cs.getPropertyValue("--acc-2").trim());
    } catch {}
  };
  readColors();
  new MutationObserver(readColors).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

  let pw = 0;
  let ph = 0;
  let dots = [];
  const mouse = { x: -1e4, y: -1e4 };
  const resize = () => {
    const dpr = Math.min(devicePixelRatio || 1, 2);
    pw = innerWidth;
    ph = innerHeight;
    particlesEl.width = pw * dpr;
    particlesEl.height = ph * dpr;
    pctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const count = clamp(Math.round((pw * ph) / 16000), 34, 110);
    dots = Array.from({ length: count }, () => ({
      x: Math.random() * pw,
      y: Math.random() * ph,
      vx: (Math.random() - 0.5) * 0.34,
      vy: (Math.random() - 0.5) * 0.34,
      r: Math.random() * 1.5 + 0.7,
      c: Math.random() > 0.55 ? 1 : 0,
    }));
  };
  resize();
  let rzT = 0;
  addEventListener("resize", () => { clearTimeout(rzT); rzT = setTimeout(resize, 160); });

  let running = true;
  const frame = () => {
    const light = document.documentElement.dataset.theme === "light";
    const link = 120;
    pctx.clearRect(0, 0, pw, ph);
    for (const d of dots) {
      d.x += d.vx;
      d.y += d.vy;
      const dx = d.x - mouse.x;
      const dy = d.y - mouse.y;
      const md = Math.hypot(dx, dy);
      if (md < 130 && md > 0.01) { d.x += (dx / md) * 0.7; d.y += (dy / md) * 0.7; }
      if (d.x < -24) d.x = pw + 24; else if (d.x > pw + 24) d.x = -24;
      if (d.y < -24) d.y = ph + 24; else if (d.y > ph + 24) d.y = -24;
    }
    pctx.lineWidth = 1;
    for (let i = 0; i < dots.length; i++) {
      const a = dots[i];
      for (let j = i + 1; j < dots.length; j++) {
        const b = dots[j];
        const ddx = a.x - b.x;
        const ddy = a.y - b.y;
        const d2 = ddx * ddx + ddy * ddy;
        if (d2 < link * link) {
          const al = (1 - Math.sqrt(d2) / link) * (light ? 0.22 : 0.13);
          pctx.strokeStyle = `rgba(${cA[0]},${cA[1]},${cA[2]},${al.toFixed(3)})`;
          pctx.beginPath();
          pctx.moveTo(a.x, a.y);
          pctx.lineTo(b.x, b.y);
          pctx.stroke();
        }
      }
      const mdx = a.x - mouse.x;
      const mdy = a.y - mouse.y;
      const mdist = Math.hypot(mdx, mdy);
      if (mdist < 160) {
        const al = (1 - mdist / 160) * (light ? 0.32 : 0.24);
        pctx.strokeStyle = `rgba(${cB[0]},${cB[1]},${cB[2]},${al.toFixed(3)})`;
        pctx.beginPath();
        pctx.moveTo(a.x, a.y);
        pctx.lineTo(mouse.x, mouse.y);
        pctx.stroke();
      }
      const col = a.c ? cB : cA;
      pctx.fillStyle = `rgba(${col[0]},${col[1]},${col[2]},${light ? 0.45 : 0.5})`;
      pctx.beginPath();
      pctx.arc(a.x, a.y, a.r, 0, Math.PI * 2);
      pctx.fill();
    }
    if (running) requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) running = false;
    else if (!running) { running = true; requestAnimationFrame(frame); }
  });
  addEventListener("pointermove", (e) => { mouse.x = e.clientX; mouse.y = e.clientY; }, { passive: true });
  document.addEventListener("mouseleave", () => { mouse.x = -1e4; mouse.y = -1e4; });
}

const glowEls = $$(".glow");
if (glowEls.length && !reducedMotion) {
  let tx = 0, ty = 0, cx = 0, cy = 0;
  addEventListener("pointermove", (e) => {
    tx = e.clientX / innerWidth - 0.5;
    ty = e.clientY / innerHeight - 0.5;
  }, { passive: true });
  const loop = () => {
    cx += (tx - cx) * 0.045;
    cy += (ty - cy) * 0.045;
    glowEls.forEach((g, i) => {
      const dir = i === 0 ? 1 : -1;
      g.style.transform = `translate(${(cx * 70 * dir).toFixed(1)}px, ${(cy * 46 * dir).toFixed(1)}px)`;
    });
    requestAnimationFrame(loop);
  };
  requestAnimationFrame(loop);
}

if (cursor) {
  addEventListener("pointerover", (e) => {
    const t = e.target;
    cursor.classList.toggle("lit", !!(t && t.closest && t.closest("a, button, summary, input, .icon")));
  }, { passive: true });
}

if (!reducedMotion && finePointer.matches) {
  for (const card of $$(".pillar, .arch-card, .code-card, .steps li")) {
    card.classList.add("tilt");
    let raf = 0;
    card.addEventListener("pointerenter", () => card.classList.add("tilting"));
    card.addEventListener("pointermove", (e) => {
      const r = card.getBoundingClientRect();
      const px = (e.clientX - r.left) / r.width;
      const py = (e.clientY - r.top) / r.height;
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        card.style.setProperty("--mx", `${(px * 100).toFixed(1)}%`);
        card.style.setProperty("--my", `${(py * 100).toFixed(1)}%`);
        card.style.setProperty("--rx", `${((0.5 - py) * 4.5).toFixed(2)}deg`);
        card.style.setProperty("--ry", `${((px - 0.5) * 4.5).toFixed(2)}deg`);
      });
    });
    card.addEventListener("pointerleave", () => {
      card.classList.remove("tilting");
      cancelAnimationFrame(raf);
      card.style.setProperty("--rx", "0deg");
      card.style.setProperty("--ry", "0deg");
    });
  }
}

if (anime && !reducedMotion && finePointer.matches) {
  for (const btn of $$(".btn")) {
    btn.style.transition = "box-shadow 0.2s, background 0.3s, border-color 0.3s";
    btn.addEventListener("pointermove", (e) => {
      const r = btn.getBoundingClientRect();
      anime.set(btn, { translateX: (e.clientX - (r.left + r.width / 2)) * 0.16, translateY: (e.clientY - (r.top + r.height / 2)) * 0.24 });
    });
    btn.addEventListener("pointerleave", () => {
      anime({ targets: btn, translateX: 0, translateY: 0, duration: 520, easing: "spring(1, 78, 12, 0)" });
    });
  }
}

const boom = (x, y, big) => {
  if (!confetti || reducedMotion) return;
  confetti({
    particleCount: big ? 80 : 26,
    spread: big ? 72 : 55,
    startVelocity: big ? 34 : 20,
    gravity: 0.9,
    scalar: big ? 0.9 : 0.7,
    ticks: 130,
    colors: ["#8ab4ff", "#c4b5fd", "#6ee7d8", "#ffffff"],
    origin: { x: x / innerWidth, y: y / innerHeight },
    zIndex: 3000,
    disableForReducedMotion: true,
  });
};

for (const btn of $$(".btn-primary")) {
  btn.addEventListener("click", (e) => {
    const r = btn.getBoundingClientRect();
    boom(e.clientX || (r.left + r.width / 2), e.clientY || (r.top + r.height / 2), true);
  });
}

const scanTitleEl = $("#scanTitle");
if (scanTitleEl && confetti) {
  new MutationObserver(() => {
    if (scanTitleEl.textContent === t("result.done")) {
      const r = scanTitleEl.getBoundingClientRect();
      boom(r.left + r.width / 2, r.top + 10, false);
    }
  }).observe(scanTitleEl, { childList: true, characterData: true, subtree: true });
}

const heroH1 = $(".hero h1");
if (heroH1 && !reducedMotion) {
  const splitNode = (node) => {
    if (node.nodeType === Node.TEXT_NODE) {
      const frag = document.createDocumentFragment();
      for (const ch of node.textContent) {
        if (!ch.trim()) { frag.append(ch); continue; }
        const s = document.createElement("span");
        s.className = "ch";
        s.textContent = ch;
        frag.append(s);
      }
      node.replaceWith(frag);
    } else if (node.nodeType === Node.ELEMENT_NODE && node.tagName !== "BR") {
      [...node.childNodes].forEach(splitNode);
    }
  };
  [...heroH1.childNodes].forEach(splitNode);
  if (anime && introFresh) {
    const chs = $$(".ch", heroH1);
    anime.set(".hero-kicker, .hero .lead, .hero-actions, .hero-stats", { opacity: 0, translateY: 14 });
    anime.set(chs, { opacity: 0, translateY: "0.55em", rotate: "-7deg" });
    const tl = anime.timeline({ easing: "easeOutExpo" });
    tl.add({ targets: ".hero-kicker", opacity: [0, 1], translateY: [14, 0], duration: 620 })
      .add({ targets: chs, opacity: [0, 1], translateY: ["0.55em", 0], rotate: ["-7deg", 0], duration: 720, delay: anime.stagger(26) }, "-=340")
      .add({ targets: ".hero .lead", opacity: [0, 1], translateY: [14, 0], duration: 620 }, "-=380")
      .add({ targets: ".hero-actions", opacity: [0, 1], translateY: [14, 0], duration: 620 }, "-=440")
      .add({ targets: ".hero-stats", opacity: [0, 1], translateY: [14, 0], duration: 620 }, "-=440");
  }
}

if (!reducedMotion) {
  const glyphs = () => t("grad.glyphs");
  const hold = async (el, ms) => {
    const end = performance.now() + ms;
    do {
      await wait(90);
    } while (el.matches(":hover") || performance.now() < end);
  };
  const scrambleTo = (el, word) => new Promise((resolve) => {
    let settled = 0;
    const tick = () => {
      if (el.matches(":hover")) settled = word.length;
      else if (Math.random() < 0.75) settled += 1;
      let out = "";
      const g = glyphs();
      for (let i = 0; i < word.length; i++) {
        out += i < settled ? word[i] : g[(Math.random() * g.length) | 0];
      }
      el.textContent = out;
      if (settled >= word.length) { el.textContent = word; resolve(); return; }
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
  let gradCycle = 0;
  const startGradCycle = async () => {
    const id = ++gradCycle;
    const el = $(".hero .grad");
    if (!el) return;
    const words = t("grad.words");
    el.style.minWidth = "0px";
    let maxW = 0;
    for (const w of words) {
      el.textContent = w;
      maxW = Math.max(maxW, el.offsetWidth);
    }
    el.textContent = words[0];
    el.style.minWidth = maxW + "px";
    let i = 0;
    while (id === gradCycle) {
      await hold(el, 700);
      if (id !== gradCycle) break;
      i = (i + 1) % words.length;
      await scrambleTo(el, words[i]);
    }
  };
  window.restartGrad = startGradCycle;
  setTimeout(startGradCycle, 3600);
}

const statEls = $$(".hero-stats strong");
if (statEls.length && !reducedMotion) {
  const rollStat = (el) => {
    const raw = el.textContent.trim();
    const num = parseInt(raw, 10);
    if (!Number.isFinite(num)) return;
    const suffix = raw.replace(/^-?\d+/, "");
    if (num === 0) {
      let n = 0;
      const id = setInterval(() => {
        n += 1;
        el.textContent = n > 9 ? "0" : String((Math.random() * 90 + 9) | 0);
        if (n > 9) clearInterval(id);
      }, 55);
      return;
    }
    if (!anime) return;
    const counter = { v: 0 };
    anime({ targets: counter, v: num, round: 1, duration: 1600, easing: "easeOutExpo", update: () => { el.textContent = Math.round(counter.v) + suffix; } });
  };
  const statsBox = $(".hero-stats");
  const run = () => setTimeout(() => statEls.forEach(rollStat), anime && introFresh ? 2100 : 0);
  if ("IntersectionObserver" in window && statsBox) {
    const io = new IntersectionObserver((entries) => {
      if (entries.some((en) => en.isIntersecting)) { run(); io.disconnect(); }
    }, { threshold: 0.3 });
    io.observe(statsBox);
  } else run();
}