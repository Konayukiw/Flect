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
    { t: "リサイズ", s: "resize" },
    { t: "圧縮", s: "compress" },
    { t: "回転", s: "rotate" },
    { t: "変換", s: "convert", sub: true },
    { t: "OCR", s: "ocr" },
  ],
  imageSub: [
    { fmt: "PNG" }, { fmt: "JPG" }, { fmt: "WEBP" }, { fmt: "HEIC" }, { fmt: "ICO" }, { fmt: "GIF" }, { fmt: "SVG" },
  ],
  video: [
    { t: "リサイズ", s: "resize" },
    { t: "トリム", s: "trim" },
    { t: "サムネイル", s: "thumb" },
    { t: "圧縮", s: "compress" },
    { t: "回転", s: "rotate" },
    { t: "変換", s: "convert", sub: true },
    { t: "音声を抽出", s: "audio" },
  ],
  videoSub: [
    { fmt: "MP4" }, { fmt: "MOV" }, { fmt: "MKV" }, { fmt: "M4A" }, { fmt: "AVI" }, { fmt: "WEBM" }, { fmt: "FLV" }, { fmt: "GIF" },
  ],
  audio: [
    { t: "変換", s: "convert" },
  ],
  pdf: [
    { t: "プレビュー", s: "preview" },
    { t: "結合", s: "merge" },
    { t: "変換", s: "convert", sub: true },
  ],
  pdfSub: [
    { fmt: "PNG" }, { fmt: "JPG" }, { fmt: "TXT" },
  ],
  folder: [
    { t: "重複を削除...", s: "dup" },
    { t: "空フォルダを削除", s: "dup" },
    { t: "ツリー表示", s: "tree" },
    { t: "名前を一括変更...", s: "rename" },
    { t: "解析", s: "analyze" },
    { t: "圧縮", s: "compress" },
  ],
  archive: [
    { t: "展開", s: "extract" },
  ],
  text: [
    { t: "整形", s: "sort" },
    { t: "キーを並べ替え", s: "sort" },
  ],
};

const TASKS = {
  "リサイズ (image)": { scans: [["写真を解析しています…", "45%"], ["リサイズしています…", "78%"]], job: "resize" },
  "圧縮 (image)": { scans: [["写真を解析しています…", "40%"], ["圧縮しています…", "72%"]], job: "compress" },
  "回転 (image)": { scans: [["回転しています…", "80%"]], job: "compress" },
  "OCR (image)": { scans: [["OCR を実行しています…", "55%"]], job: "ocr" },
  "リサイズ (video)": { scans: [["動画を解析しています…", "30%"], ["リサイズしています…", "75%"]], job: "compress" },
  "トリム (video)": { scans: [["トリムしています（再エンコードなし）…", "95%"]], job: "compress" },
  "サムネイル (video)": { scans: [["フレームを書き出しています…", "80%"]], job: "convert" },
  "圧縮 (video)": { scans: [["動画を解析しています…", "25%"], ["圧縮しています…", "70%"]], job: "compress" },
  "回転 (video)": { scans: [["回転しています…", "60%"]], job: "compress" },
  "音声を抽出 (video)": { scans: [["音声を抽出しています…", "66%"], ["書き出しています…", "88%"]], job: "compress" },
  "変換 (image)": { scans: [["書き出しています…", "64%"]], job: "convert" },
  "変換 (video)": { scans: [["変換しています…", "50%"]], job: "convert" },
  "変換 (audio)": { scans: [["変換しています…", "70%"]], job: "convert" },
  "プレビュー (pdf)": { scans: [["プレビューを開いています…", "40%"]], job: "convert" },
  "結合 (pdf)": { scans: [["PDF を結合しています…", "75%"]], job: "convert" },
  "変換 (pdf)": { scans: [["書き出しています…", "82%"]], job: "convert" },
  "重複を削除... (folder)": { scans: [["フォルダをスキャンしています…", "35%"], ["ハッシュを比較しています…", "70%"], ["ごみ箱へ移動しています…", "92%"]], job: "duplicate" },
  "空フォルダを削除 (folder)": { scans: [["スキャンしています…", "55%"], ["ごみ箱へ移動しています…", "90%"]], job: "duplicate" },
  "ツリー表示 (folder)": { scans: [["ツリーを生成しています…", "80%"]], job: "convert" },
  "名前を一括変更... (folder)": { scans: [["名前を変更しています…", "76%"]], job: "convert" },
  "解析 (folder)": { scans: [["解析中…", "50%"], ["レポートを作成しています…", "88%"]], job: "duplicate" },
  "圧縮 (folder)": { scans: [["フォルダを圧縮しています…", "70%"]], job: "compress" },
  "展開 (archive)": { scans: [["展開しています…", "65%"], ["ファイルを書き出しています…", "88%"]], job: "convert" },
};

const RESULTS = {
  duplicate: { cls: "ic-ok", bar: "60%", title: "完了しました", desc: "12 件の重複を見つけ、ごみ箱に移動しました。" },
  resize: { cls: "ic-ok", bar: "100%", title: "完了しました", desc: "新規ファイルとして保存されました。元のファイルは変更されていません。" },
  compress: { cls: "ic-ok", bar: "100%", title: "完了しました", desc: "目標サイズに収まるよう調整し、新しいファイルとして保存しました。" },
  convert: { cls: "ic-ok", bar: "100%", title: "完了しました", desc: "変換結果は元のファイルの隣に、新しいファイルとして作成されました。" },
  ocr: { cls: "ic-ok", bar: "100%", title: "完了しました", desc: "文字を検出し、クリップボードにコピーしました。出力先は設定で変更できます。" },
};

const applyTheme = (t) => { document.documentElement.dataset.theme = t; };
try {
  const saved = localStorage.getItem("flect-theme");
  applyTheme(saved === "light" || saved === "dark"
    ? saved
    : matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark");
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

const runTask = async (kind, label) => {
  const t = TASKS[`${label} (${kind})`];
  if (t) {
    await runScan((t.scans || []).map(([l, p]) => [l, p]));
    await finishScan(true, t.job);
    return;
  }
  await runScan([[`${label}の設定を適用しています…`, "70%"]]);
  await finishScan(true, "convert");
};

const renderMenu = (name, kind) => {
  const body = $("#ctxBody");
  let html = headerHtml(name, kind);
  html += `<div class="ctx-sep"></div>`;
  for (const it of menuFor(kind)) {
    html += `<button class="ctx-item" type="button" data-label="${it.t}">${itemSvg(it.s)}`
      + `<span class="ct-lbl">${it.t}</span>${it.sub ? '<span class="ct-sub">›</span>' : ""}</button>`;
  }
  html += `<div class="ctx-sep"></div>`;
  html += `<button class="ctx-item" type="button" data-label="カスタム…">${itemSvg("sort")}<span class="ct-lbl">カスタム…</span></button>`;
  body.innerHTML = html;
  attachHandlers(body, name, kind, "main");
};

const renderSubMenu = (name, kind) => {
  const body = $("#ctxBody");
  const subs = subFor(kind);
  let html = headerHtml(name, kind);
  html += `<div class="ctx-sep"></div>`;
  html += `<button class="ctx-item" type="button" data-back="1">${itemSvg("resize")}<span class="ct-lbl">← 戻る</span></button>`;
  html += `<div class="ctx-sep"></div>`;
  for (const f of subs) {
    html += `<button class="ctx-item" type="button" data-to="${f.fmt}"><span class="ct-ic">${icons.convert}</span><span class="ct-lbl">to ${f.fmt}</span></button>`;
  }
  body.innerHTML = html;

  for (const btn of $$(".ctx-item", body)) {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const to = btn.dataset.to;
      const back = btn.dataset.back;
      if (back) { renderMenu(name, kind); return; }
      resetMenu();
      runScan([["変換 (" + to + ") に書き出しています…", "60%"]])
        .then(() => finishScan(true, "convert"));
    });
  }
};

const attachHandlers = (body, name, kind, mode) => {
  for (const btn of $$(".ctx-item", body)) {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const label = btn.dataset.label;
      if (!label) return;
      resetMenu();
      if (mode === "main" && label === "変換" && subFor(kind).length) {
        renderSubMenu(name, kind);
        openMenuAt(parseFloat(menuX) || 320, parseFloat(menuY) || 320);
        return;
      }
      runTask(kind, label);
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
  okBtn.title = "確定";
  const cancelBtn = document.createElement("button");
  cancelBtn.textContent = "✕";
  cancelBtn.title = "キャンセル";
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
  body.innerHTML = headerHtml("デスクトップ", "desk")
    + '<div class="ctx-sep"></div>'
    + `<button class="ctx-item" type="button" data-row="1">${itemSvg("resize")}<span class="ct-lbl">設定を開く</span></button>`
    + `<button class="ctx-item" type="button" data-row="2">${itemSvg("tree")}<span class="ct-lbl">この画面について</span></button>`;
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
  $("#scanTitle").textContent = ok ? r.title : "失敗しました";
  $("#scanDesc").textContent = ok ? r.desc : "作成中のファイルを削除して終了しました。";
  $("#scanBar").style.transition = "none";
  $("#scanBar").style.width = (ok ? r.bar : "0%");
  setTimeout(() => {
    const ov = $("#scanOverlay");
    ov.classList.remove("open");
    setTimeout(() => { icon.className = "scan-icon"; $("#scanTitle").textContent = "処理を開始します"; $("#scanDesc").textContent = ""; resolve(); }, 380);
  }, 2100);
});

const failScan = () => {
  $("#scanOverlay").classList.add("open");
  $("#scanTitle").textContent = "失敗しました";
  $("#scanDesc").textContent = "これはデモです。Flect は、失敗・キャンセル時に作成中のファイルを自動で削除します。";
  const icon = $("#scanOverlay .scan-icon");
  icon.className = "scan-icon ic-fail";
  $("#scanBar").style.transition = "none";
  $("#scanBar").style.width = "0%";
  setTimeout(() => {
    $("#scanOverlay").classList.remove("open");
    setTimeout(() => { icon.className = "scan-icon"; $("#scanTitle").textContent = "処理を開始します"; $("#scanDesc").textContent = ""; }, 380);
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
        <span class="modal-title">Flect — このデモについて</span>
        <button class="modal-x" type="button" aria-label="閉じる">✕</button>
      </div>
      <div class="modal-body">
        <p>このデスクトップは、Flect の動作イメージを再現した<strong>デモ</strong>です。実物の右クリックメニューではないため、項目名や挙動は実際のものと異なることがあります。</p>
        <p>Flect は、選択したファイルの種類に応じた項目だけがエクスプローラーのメニューに表示されます。メニュー項目の追加・非表示は設定アプリの「一般 → 右クリックメニューの項目」から変更できます。</p>
        <p class="modal-tip">デスクトップの何もない場所をもう一度右クリックすると、この項目を再び開けます。</p>
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
    if (scanTitleEl.textContent === "完了しました") {
      const r = scanTitleEl.getBoundingClientRect();
      boom(r.left + r.width / 2, r.top + 10, false);
    }
  }).observe(scanTitleEl, { childList: true, characterData: true, subtree: true });
}

const heroH1 = $(".hero h1");
const gradSpan = $(".hero .grad");
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

if (gradSpan && !reducedMotion) {
  const words = ["万能ツール", "画像変換", "動画圧縮", "重複削除", "PDF結合", "OCR"];
  const glyphs = "アイウエオカキクケコサシスセソタチツテトナニヌネノ01+*#";
  const hold = async (ms) => {
    const end = performance.now() + ms;
    do {
      await wait(90);
    } while (gradSpan.matches(":hover") || performance.now() < end);
  };
  const scrambleTo = (word) => new Promise((resolve) => {
    let settled = 0;
    const tick = () => {
      if (gradSpan.matches(":hover")) settled = word.length;
      else if (Math.random() < 0.75) settled += 1;
      let out = "";
      for (let i = 0; i < word.length; i++) {
        out += i < settled ? word[i] : glyphs[(Math.random() * glyphs.length) | 0];
      }
      gradSpan.textContent = out;
      if (settled >= word.length) { gradSpan.textContent = word; resolve(); return; }
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
  const startCycle = async () => {
    gradSpan.textContent = words[0];
    gradSpan.style.minWidth = gradSpan.offsetWidth + "px";
    let i = 0;
    while (true) {
      await hold(700);
      i = (i + 1) % words.length;
      await scrambleTo(words[i]);
    }
  };
  setTimeout(startCycle, 3600);
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