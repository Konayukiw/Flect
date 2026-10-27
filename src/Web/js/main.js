const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const wait = (ms) => new Promise((r) => setTimeout(r, ms));

const applyTheme = (th) => { document.documentElement.dataset.theme = th; };
try {
  const saved = localStorage.getItem("flect-theme");
  applyTheme(saved === "light" || saved === "dark" ? saved : "light");
  $("#themeToggle").addEventListener("click", () => {
    const next = document.documentElement.dataset.theme === "dark" ? "light" : "light";
    applyTheme(next);
    try { localStorage.setItem("flect-theme", next); } catch {}
  });
} catch {}

try {
  const { hostname, pathname, protocol } = location;
  if (protocol === "https:" && /\.github\.io$/i.test(hostname) && /^\/Flect\//i.test(pathname)) {
    const badge = $(".dev-badge");
    badge.textContent = "under development";
    badge.classList.add("show");
  }
} catch {}

const island = $("#dynamicIsland");
const navLinks = $$(".nav-links a");
const trackedSections = navLinks.map((a) => $(a.getAttribute("href"))).filter(Boolean);

const syncIsland = () => {
  if (!island) return;
  const scrolled = scrollY > 24;
  island.classList.toggle("compact", scrolled);
  if (!scrolled) island.classList.remove("expanded");

  let currentId = "";
  const probe = scrollY + innerHeight * 0.28;
  for (const sec of trackedSections) {
    if (sec.offsetTop <= probe) currentId = `#${sec.id}`;
  }
  for (const link of navLinks) {
    link.classList.toggle("active", link.getAttribute("href") === currentId);
  }
};

addEventListener("scroll", syncIsland, { passive: true });
addEventListener("resize", syncIsland);
syncIsland();

if (island) {
  island.addEventListener("pointerleave", () => {
    island.classList.remove("expanded");
  });
}

const revealEls = $$(".reveal");
if ("IntersectionObserver" in window) {
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
    }
  }, { threshold: 0.1 });
  revealEls.forEach((el) => io.observe(el));
} else {
  revealEls.forEach((el) => el.classList.add("in"));
}

const desk = $("#desk");
const deskWrap = desk.parentElement;
const iconsEl = $("#deskIcons");

const MiB = 1048576;
const SEP = "-";
const BAD_NAME = /[\\/:*?"<>|]/;
const STRIP_NAME = /[\\/:*?"<>|\r\n]/g;

const IMAGE_FORMATS = ["PNG", "JPG", "WEBP", "HEIC", "ICO", "GIF", "SVG"];
const VIDEO_FORMATS = ["MP4", "MOV", "MKV", "M4A", "AVI", "WEBM", "FLV"];
const AUDIO_FORMATS = ["MP3", "M4A", "WAV", "AIF", "AIFF", "AAC", "OGG", "WMA"];
const IMAGE_EXT = { png: "PNG", jpg: "JPG", jpeg: "JPG", webp: "WEBP", heic: "HEIC", heif: "HEIC", ico: "ICO", gif: "GIF", svg: "SVG" };
const VIDEO_EXT = { mp4: "MP4", mov: "MOV", mkv: "MKV", m4a: "M4A", avi: "AVI", webm: "WEBM", flv: "FLV" };
const AUDIO_EXT = { mp3: "MP3", wav: "WAV", aif: "AIF", aiff: "AIFF", aac: "AAC", ogg: "OGG", wma: "WMA" };
const ARCHIVE_EXT = { zip: "ZIP", "7z": "7Z", tar: "TAR", rar: "RAR" };
const TEXT_FORMAT = { txt: "TXT", text: "TXT", json: "JSON", csv: "CSV", xml: "XML", ini: "INI", cfg: "CFG", conf: "CFG", css: "CSS", html: "HTML", htm: "HTML", md: "MD", markdown: "MD" };
const TEXT_EXT = new Set("txt text md markdown log csv tsv ini cfg conf json xml yaml yml toml html htm css js mjs cjs ts tsx jsx c h cpp hpp cc hh cs java kt py rb go rs php pl lua sh bash bat cmd ps1 psm1 sql srt vtt asc tex rst env props properties gradle cmake m mm swift vb r jl".split(" "));
const IMAGE_WEIGHT = { PNG: 2.4, JPG: 0.55, WEBP: 0.35, HEIC: 0.3, ICO: 0.012, GIF: 0.9, SVG: 1.7 };
const VIDEO_WEIGHT = { MP4: 1, MOV: 1.12, MKV: 1, M4A: 0.006, AVI: 1.5, WEBM: 0.72, FLV: 0.85 };
const AUDIO_WEIGHT = { MP3: 1, M4A: 0.95, WAV: 7.5, AIF: 7.5, AIFF: 7.5, AAC: 0.92, OGG: 0.82, WMA: 0.93 };
const LOOK = { folder: "folder", image: "image", video: "video", audio: "audio", pdf: "pdf", archive: "zip", text: "text", jar: "file", none: "file" };

const ICONS = {
  folder: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h7l2 2h9v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z" fill="currentColor"/></svg>',
  image: '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2" fill="none" stroke="currentColor" stroke-width="1.8"/><circle cx="9" cy="10" r="1.6" fill="currentColor"/><path d="M3 17l5-5 4 4 3-3 6 6" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  video: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 7l9 5-9 5V7zM13 7h7v10h-7z" fill="currentColor"/></svg>',
  audio: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 17.5V6l11-2v11.5" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><circle cx="6.5" cy="17.5" r="2.5" fill="currentColor"/><circle cx="17.5" cy="15.5" r="2.5" fill="currentColor"/></svg>',
  pdf: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 2h8l4 4v16H6z" fill="currentColor"/><path d="M14 2v4h4" fill="none" stroke="var(--bg)" stroke-width="1.4"/><rect x="9" y="10" width="6" height="1.6" rx="0.8" fill="var(--bg)"/><rect x="9" y="13" width="6" height="1.6" rx="0.8" fill="var(--bg)"/></svg>',
  zip: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3h12a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z" fill="none" stroke="currentColor" stroke-width="1.8"/><path d="M9 3v3h3V3h3v3h3" stroke="currentColor" stroke-width="1.4" fill="none"/></svg>',
  text: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 2h8l4 4v16H6z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><path d="M9 11h6M9 14h6M9 17h4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  file: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 2h8l4 4v16H6z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><path d="M14 2v4h4" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/></svg>',
};
const MINI = {
  folder: '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M1.5 3.5h4.5l1.5 1.5h7v8h-13z" fill="#f4c35a"/></svg>',
  text: '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 1.5h6l3 3v10h-9z" fill="#fff" stroke="#8a8f98"/><path d="M5.5 7.5h5M5.5 9.5h5M5.5 11.5h3" stroke="#8a8f98"/></svg>',
};
const CHEVRON = '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M6 3.5L10.5 8 6 12.5" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>';
const CROSS = '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/></svg>';

const CONTENT = {
  club: [["2024-07/IMG_0012.jpg", 4.9], ["2024-07/IMG_0012 (1).jpg", 4.9, 1], ["2024-07/IMG_0013.jpg", 5.2], ["2024-11/raw/"], ["2024-11/DSC_0101.jpg", 6.1], ["2024-11/clip_01.mov", 182], ["_old/"], ["group.png", 3.4]],
  training: [["Day1/orientation.pdf", 2.4], ["Day1/welcome.mp4", 96], ["Day2/old/"], ["Day2/security.pptx", 8.2], ["Day2/quiz.docx", 0.1], ["Day2/quiz (1).docx", 0.1, 1], ["drafts/"], ["handouts/"], ["schedule.pdf", 0.3]],
  warehouse: [["inventory/2024-Q3.csv", 0.8], ["inventory/2024-Q4.csv", 0.9], ["inventory/2024-Q4 (1).csv", 0.9, 1], ["photos/old/"], ["photos/shelf_A.jpg", 3.8], ["photos/shelf_B.jpg", 4.1], ["photos/walkthrough.mp4", 322], ["checklist.xlsx", 0.2]],
};

const pick = (table, key) => (Object.hasOwn(table, key) ? table[key] : "");

const splitName = (name) => {
  const dot = name.lastIndexOf(".");
  return dot > 0 ? [name.slice(0, dot), name.slice(dot + 1)] : [name, ""];
};

const derive = (name, suffix, ext) => {
  const [stem, current] = splitName(name);
  const next = ext ?? current;
  return stem + suffix + (next ? "." + next : "");
};

const archiveStem = (name) => (name.toLowerCase().endsWith(".tar.gz") ? name.slice(0, -7) : splitName(name)[0]);
const bare = (label) => label.replace(/(\.\.\.|…)$/, "");
const bytesOf = (icon) => Number(icon.dataset.bytes) || 0;

const fmtBytes = (bytes) => {
  if (bytes < 1024) return `${Math.round(bytes)} B`;
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  let text = value.toFixed(value >= 100 ? 0 : value >= 10 ? 1 : 2);
  if (text.includes(".")) text = text.replace(/0+$/, "").replace(/\.$/, "");
  return `${text} ${units[unit]}`;
};

const classify = (icon) => {
  if (icon.dataset.kind === "folder") return { cat: "folder", fmt: "" };
  const name = icon.dataset.name.toLowerCase();
  const dot = name.lastIndexOf(".");
  const ext = dot < 0 ? "" : name.slice(dot + 1);
  if (!ext) return { cat: "none", fmt: "" };
  if (ext === "pdf") return { cat: "pdf", fmt: "PDF" };
  if (ext === "jar") return { cat: "jar", fmt: "JAR" };
  if (name.length > 7 && name.endsWith(".tar.gz")) return { cat: "archive", fmt: "TAR.GZ" };
  if (pick(ARCHIVE_EXT, ext)) return { cat: "archive", fmt: ARCHIVE_EXT[ext] };
  if (pick(IMAGE_EXT, ext)) return { cat: "image", fmt: IMAGE_EXT[ext] };
  if (pick(VIDEO_EXT, ext)) return { cat: "video", fmt: VIDEO_EXT[ext] };
  if (pick(AUDIO_EXT, ext)) return { cat: "audio", fmt: AUDIO_EXT[ext] };
  if (TEXT_EXT.has(ext)) return { cat: "text", fmt: pick(TEXT_FORMAT, ext) || "TEXT" };
  return { cat: "none", fmt: "" };
};

const textTargets = (from) => {
  const targets = from === "TXT" ? [] : ["TXT"];
  if (from === "CSV") targets.push("JSON", "XML", "HTML", "MD");
  else if (from === "JSON") targets.push("CSV", "XML");
  else if (from === "XML") targets.push("JSON");
  else if (from === "INI" || from === "CFG") targets.push("JSON", "XML");
  return targets;
};

const paint = (icon) => {
  const look = LOOK[classify(icon).cat];
  const ic = icon.querySelector(".ic");
  ic.className = `ic ic-${look}`;
  ic.innerHTML = ICONS[look];
};

const contents = new WeakMap();
const contentOf = (icon) => contents.get(icon) || [];

const statsOf = (entries) => {
  const files = entries.filter(([path]) => !path.endsWith("/"));
  const dirs = new Set();
  for (const [path] of entries) {
    const parts = path.split("/");
    for (let i = 1; i < parts.length; i++) dirs.add(parts.slice(0, i).join("/"));
  }
  const dupes = files.filter((entry) => entry[2]);
  const sum = (list) => list.reduce((total, entry) => total + entry[1] * MiB, 0);
  return { files: files.length, folders: dirs.size, bytes: sum(files), dupes: dupes.length, dupeBytes: sum(dupes), empties: entries.length - files.length, list: files };
};

const treeText = (root, entries) => {
  const top = { dirs: new Map(), files: [] };
  for (const [path] of entries) {
    const parts = path.split("/");
    const last = parts.pop();
    let node = top;
    for (const part of parts) {
      if (!node.dirs.has(part)) node.dirs.set(part, { dirs: new Map(), files: [] });
      node = node.dirs.get(part);
    }
    if (last) node.files.push(last);
  }
  const lines = [root];
  const walk = (node, pad) => {
    const kids = [...node.dirs, ...node.files.map((name) => [name, null])];
    kids.forEach(([name, child], i) => {
      const end = i === kids.length - 1;
      lines.push(`${pad}${end ? "└─ " : "├─ "}${name}`);
      if (child) walk(child, pad + (end ? "   " : "│  "));
    });
  };
  walk(top, "");
  return lines.join("\n");
};

const items = () => $$(".icon:not(.gone)", iconsEl);
const selected = () => items().filter((icon) => icon.classList.contains("selected"));
let anchor = null;

const select = (list) => {
  const keep = new Set(list);
  for (const icon of items()) {
    const on = keep.has(icon);
    icon.classList.toggle("selected", on);
    icon.setAttribute("aria-selected", String(on));
  }
};

const focusIcon = (icon, reveal = false) => {
  for (const other of items()) other.tabIndex = other === icon ? 0 : -1;
  icon.focus({ preventScroll: true });
  if (reveal) icon.scrollIntoView({ block: "nearest" });
};

const nameTaken = (name, except) => {
  const lower = name.toLowerCase();
  return items().some((icon) => icon !== except && icon.dataset.name.toLowerCase() === lower);
};

const uniqueName = (stem, ext, except = null) => {
  const tail = ext ? "." + ext : "";
  let name = stem + tail;
  for (let n = 2; nameTaken(name, except); n++) name = `${stem} (${n})${tail}`;
  return name;
};

const makeIcon = ({ name, folder = false, bytes = 0, pages = 0, content = null }) => {
  const icon = document.createElement("button");
  icon.type = "button";
  icon.className = "icon born";
  icon.tabIndex = items().length ? -1 : 0;
  icon.setAttribute("role", "option");
  icon.setAttribute("aria-selected", "false");
  icon.dataset.kind = folder ? "folder" : "file";
  icon.dataset.name = name;
  icon.dataset.bytes = String(Math.round(bytes));
  icon.dataset.modified = String(Date.now());
  if (pages) icon.dataset.pages = String(pages);
  icon.innerHTML = '<span class="ic"></span><span class="nm"></span>';
  icon.querySelector(".nm").textContent = name;
  if (folder || content) contents.set(icon, (content || []).map((entry) => [...entry]));
  paint(icon);
  icon.addEventListener("animationend", () => icon.classList.remove("born"), { once: true });
  iconsEl.append(icon);
  return icon;
};

const removeIcon = (icon) => {
  const focused = document.activeElement === icon;
  const roving = icon.tabIndex === 0;
  icon.classList.add("gone");
  icon.classList.remove("selected");
  setTimeout(() => icon.remove(), 220);
  if (anchor === icon) anchor = null;
  const rest = items();
  if (roving && rest.length) rest[0].tabIndex = 0;
  if (focused) iconsEl.focus({ preventScroll: true });
};

const setName = (icon, name) => {
  icon.dataset.name = name;
  icon.removeAttribute("data-i18n-name");
  icon.querySelector(".nm").textContent = name;
  paint(icon);
};

const menus = [];
let menuTimer = 0;

const accessKey = (label) => (/\(([A-Z0-9])\)$/.exec(label) || [])[1] || "";

const setHot = (menu, index) => {
  menu.hot = index;
  menu.rows.forEach((row, i) => row && row.classList.toggle("hot", i === index));
};

const closeMenus = (depth = 0) => {
  clearTimeout(menuTimer);
  while (menus.length > depth) menus.pop().el.remove();
  if (menus.length) menus[menus.length - 1].child = -1;
};

const moveHot = (menu, dir) => {
  const count = menu.rows.length;
  let i = menu.hot < 0 ? (dir > 0 ? -1 : 0) : menu.hot;
  for (let k = 0; k < count; k++) {
    i = (i + dir + count) % count;
    if (menu.rows[i]) {
      setHot(menu, i);
      return;
    }
  }
};

const openSub = (menu, index, keyboard) => {
  const entry = menu.entries[index];
  const sub = openMenu(typeof entry.sub === "function" ? entry.sub() : entry.sub, 0, 0, { menu, index });
  menu.child = index;
  if (keyboard) moveHot(sub, 1);
};

const activate = (menu, index, keyboard) => {
  const entry = menu.entries[index];
  if (!entry || entry === SEP || entry.off) return;
  if (entry.sub) {
    clearTimeout(menuTimer);
    if (menu.child !== index) openSub(menu, index, keyboard);
    else if (keyboard) moveHot(menus[menu.depth + 1], 1);
    return;
  }
  closeMenus();
  entry.run?.();
};

const hoverRow = (menu, index) => {
  setHot(menu, index);
  clearTimeout(menuTimer);
  if (menu.child === index) return;
  const entry = menu.entries[index];
  const opens = entry.sub && !entry.off;
  if (!opens && menus.length <= menu.depth + 1) return;
  menuTimer = setTimeout(() => {
    closeMenus(menu.depth + 1);
    if (opens) openSub(menu, index, false);
  }, 400);
};

const openMenu = (entries, x, y, from = null) => {
  const depth = from ? from.menu.depth + 1 : 0;
  closeMenus(depth);
  const el = document.createElement("div");
  el.className = "menu";
  el.setAttribute("role", "menu");
  const menu = { el, entries, rows: [], hot: -1, child: -1, depth };
  entries.forEach((entry, index) => {
    if (entry === SEP) {
      const line = document.createElement("div");
      line.className = "menu-sep";
      line.setAttribute("role", "separator");
      el.append(line);
      menu.rows.push(null);
      return;
    }
    const row = document.createElement("div");
    row.className = "menu-item";
    row.classList.toggle("off", !!entry.off);
    row.classList.toggle("bold", !!entry.bold);
    row.setAttribute("role", "radio" in entry ? "menuitemradio" : "menuitem");
    if ("radio" in entry) row.setAttribute("aria-checked", String(entry.radio));
    if (entry.off) row.setAttribute("aria-disabled", "true");
    if (entry.sub) row.setAttribute("aria-haspopup", "menu");
    const mark = document.createElement("span");
    mark.className = "mi-ic";
    if (entry.img) mark.innerHTML = `<img src="${entry.img}" alt="">`;
    else if (entry.svg) mark.innerHTML = entry.svg;
    else if (entry.radio) mark.innerHTML = '<i class="mi-dot"></i>';
    const label = document.createElement("span");
    label.className = "mi-lbl";
    label.textContent = entry.label;
    row.append(mark, label);
    if (entry.sub) row.insertAdjacentHTML("beforeend", `<span class="mi-sub">${CHEVRON}</span>`);
    row.addEventListener("pointerenter", () => hoverRow(menu, index));
    row.addEventListener("click", () => activate(menu, index, false));
    el.append(row);
    menu.rows.push(row);
  });
  el.addEventListener("mousedown", (e) => e.preventDefault());
  el.addEventListener("contextmenu", (e) => e.preventDefault());
  el.addEventListener("pointerleave", () => {
    if (menu.child < 0) setHot(menu, -1);
  });
  if (from) {
    el.addEventListener("pointerenter", () => {
      clearTimeout(menuTimer);
      setHot(from.menu, from.index);
    });
  }
  document.body.append(el);
  const vw = document.documentElement.clientWidth;
  const vh = innerHeight;
  const w = el.offsetWidth;
  const h = el.offsetHeight;
  let left = x;
  let top = y;
  if (from) {
    const box = from.menu.el.getBoundingClientRect();
    const row = from.menu.rows[from.index].getBoundingClientRect();
    left = box.right - 3 + w > vw - 4 ? box.left - w + 3 : box.right - 3;
    top = row.top - 5;
  } else {
    if (left + w > vw - 4) left -= w;
    if (top + h > vh - 4) {
      top -= h;
      el.classList.add("up");
    }
  }
  el.style.left = `${clamp(left, 4, Math.max(4, vw - w - 4))}px`;
  el.style.top = `${clamp(top, 4, Math.max(4, vh - h - 4))}px`;
  menus.push(menu);
  void el.offsetWidth;
  el.classList.add("open");
  return menu;
};

addEventListener("pointerdown", (e) => {
  if (menus.length && !e.target.closest(".menu")) closeMenus();
}, true);
addEventListener("scroll", () => {
  if (menus.length) closeMenus();
}, { passive: true });
addEventListener("resize", () => {
  if (menus.length) closeMenus();
});
addEventListener("blur", () => {
  if (menus.length) closeMenus();
});

document.addEventListener("keydown", (e) => {
  if (!menus.length) return;
  const menu = menus[menus.length - 1];
  const key = e.key;
  if (key === "ArrowDown") moveHot(menu, 1);
  else if (key === "ArrowUp") moveHot(menu, -1);
  else if (key === "ArrowRight") {
    const entry = menu.entries[menu.hot];
    if (entry?.sub && !entry.off) activate(menu, menu.hot, true);
  } else if (key === "ArrowLeft") {
    if (menu.depth > 0) closeMenus(menu.depth);
  } else if (key === "Escape") closeMenus(menu.depth);
  else if (key === "Enter" || key === " ") {
    if (menu.hot >= 0) activate(menu, menu.hot, true);
  } else if (key.length === 1) {
    const letter = key.toUpperCase();
    const hits = menu.entries.flatMap((entry, i) => (entry !== SEP && accessKey(entry.label) === letter ? [i] : []));
    if (hits.length === 1) {
      setHot(menu, hits[0]);
      activate(menu, hits[0], true);
    } else if (hits.length) setHot(menu, hits.find((i) => i > menu.hot) ?? hits[0]);
  } else if (key !== "Tab") return;
  e.preventDefault();
  e.stopPropagation();
}, true);

let winZ = 50;
let winSeq = 0;

const dragWin = (el) => {
  const bar = el.querySelector(".win-title");
  bar.addEventListener("pointerdown", (e) => {
    if (e.button !== 0 || e.target.closest(".win-x")) return;
    const dx = e.clientX - el.offsetLeft;
    const dy = e.clientY - el.offsetTop;
    bar.setPointerCapture(e.pointerId);
    const move = (ev) => {
      el.style.left = `${clamp(ev.clientX - dx, 80 - el.offsetWidth, deskWrap.clientWidth - 80)}px`;
      el.style.top = `${clamp(ev.clientY - dy, -20, deskWrap.clientHeight - 32)}px`;
    };
    const end = () => {
      bar.removeEventListener("pointermove", move);
      bar.removeEventListener("pointerup", end);
      bar.removeEventListener("pointercancel", end);
    };
    bar.addEventListener("pointermove", move);
    bar.addEventListener("pointerup", end);
    bar.addEventListener("pointercancel", end);
  });
};

const openWin = (title, wide = false) => {
  const el = document.createElement("div");
  el.className = wide ? "win wide" : "win";
  el.setAttribute("role", "dialog");
  el.setAttribute("aria-label", title);
  el.innerHTML = `<div class="win-title"><img src="assets/Flect.png" alt=""><span class="win-name"></span><button class="win-x" type="button">${CROSS}</button></div><div class="win-body"></div>`;
  el.querySelector(".win-name").textContent = title;
  el.querySelector(".win-x").setAttribute("aria-label", t("prog.close"));
  deskWrap.append(el);
  const offset = (winSeq++ % 5) * 22;
  el.style.left = `${Math.max(8, (deskWrap.clientWidth - el.offsetWidth) / 2 - 44 + offset)}px`;
  el.style.top = `${64 + offset}px`;
  el.style.zIndex = String(++winZ);
  el.addEventListener("pointerdown", () => {
    el.style.zIndex = String(++winZ);
  });
  dragWin(el);
  const win = {
    el,
    body: el.querySelector(".win-body"),
    onClose: null,
    closed: false,
    close: () => {
      if (win.closed) return;
      win.closed = true;
      el.classList.remove("open");
      setTimeout(() => el.remove(), 180);
    },
  };
  el.querySelector(".win-x").addEventListener("click", () => (win.onClose ? win.onClose() : win.close()));
  void el.offsetWidth;
  el.classList.add("open");
  return win;
};

const node = (tag, cls, text) => {
  const el = document.createElement(tag);
  if (cls) el.className = cls;
  if (text !== undefined) el.textContent = text;
  return el;
};

const footer = (win, labels) => {
  const foot = node("div", "win-foot");
  const buttons = labels.map(([label, primary]) => {
    const btn = node("button", primary ? "win-btn primary" : "win-btn", label);
    btn.type = "button";
    foot.append(btn);
    return btn;
  });
  win.body.append(foot);
  return buttons;
};

const spawn = (out) => {
  const [stem, ext] = out.folder ? [out.name, ""] : splitName(out.name);
  const icon = makeIcon({ ...out, name: uniqueName(stem, ext) });
  icon.classList.add("writing");
  return icon;
};

const runTask = async (title, sel, plan) => {
  const win = openWin(`Flect - ${title}`);
  win.body.append(node("div", "win-head", title));
  const status = node("div", "win-status");
  const bar = node("div", "win-bar busy");
  const fill = node("i");
  bar.append(fill);
  const log = node("div", "win-log");
  win.body.append(status, bar, log);
  const [btn] = footer(win, [[t("prog.cancel")]]);
  let state = "run";
  const writing = [];
  const finish = (text, delay) => {
    state = "done";
    bar.classList.remove("busy");
    status.textContent = text;
    btn.disabled = false;
    btn.textContent = t("prog.close");
    setTimeout(win.close, delay);
  };
  const cancel = () => {
    if (state !== "run") return;
    state = "cancelling";
    btn.disabled = true;
    status.textContent = t("prog.cancelling");
  };
  btn.addEventListener("click", () => (state === "done" ? win.close() : cancel()));
  win.onClose = () => {
    cancel();
    win.close();
  };
  const running = async (ms) => {
    const end = performance.now() + ms;
    while (state === "run" && performance.now() < end) await wait(40);
    return state === "run";
  };
  const stop = async () => {
    await wait(450);
    for (const icon of writing) {
      log.append(node("div", "", t("prog.removed", { name: icon.dataset.name })));
      log.classList.add("show");
      removeIcon(icon);
    }
    finish(t("prog.cancelled"), 1800);
  };

  for (const key of plan.pre || []) {
    status.textContent = t(key);
    if (!(await running(650))) return stop();
  }
  bar.classList.remove("busy");
  const units = plan.once ? [sel[0]] : sel;
  for (const [index, source] of units.entries()) {
    status.textContent = t(plan.work) + (units.length > 1 ? ` (${index + 1}/${units.length})` : "");
    writing.push(...(plan.out ? plan.out(source).map(spawn) : []));
    const from = index / units.length;
    const span = 1 / units.length;
    const start = performance.now();
    const duration = plan.dur || 900;
    for (;;) {
      const progress = Math.min(1, (performance.now() - start) / duration);
      fill.style.width = `${(from + span * progress) * 100}%`;
      if (progress >= 1) break;
      if (!(await running(40))) return stop();
    }
    writing.splice(0).forEach((icon) => icon.classList.remove("writing"));
  }
  const summary = plan.summary ? plan.summary() : "";
  plan.apply?.();
  const delay = summary ? 2600 : 700;
  finish(summary || t("prog.done"), delay);
  if (plan.present) setTimeout(plan.present, delay - 120);
};

const treeWin = (sel) => {
  const label = t("menu.tree");
  const text = sel.map((icon) => treeText(icon.dataset.name, contentOf(icon))).join("\n\n");
  const win = openWin(`Flect - ${label}`, true);
  win.body.append(node("div", "win-head", label), node("pre", "win-pre", text));
  const [copy, close] = footer(win, [[t("prog.copy")], [t("prog.close"), true]]);
  copy.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(text);
      copy.textContent = t("prog.copied");
    } catch {}
  });
  close.addEventListener("click", win.close);
};

const analyzeWin = (sel) => {
  const label = t("menu.analyze");
  const stats = statsOf(sel.flatMap(contentOf));
  const win = openWin(`Flect - ${label}`, true);
  win.body.append(node("div", "win-head", label));
  const tiles = node("div", "an-tiles");
  for (const [value, caption] of [[fmtBytes(stats.bytes), t("an.total")], [String(stats.files), t("an.files")], [String(stats.folders), t("an.folders")], [`${(0.2 + stats.files * 0.04).toFixed(1)}s`, t("an.scanTime")]]) {
    const tile = node("div", "an-tile");
    tile.append(node("b", "", value), node("span", "", caption));
    tiles.append(tile);
  }
  win.body.append(tiles, node("div", "an-h", t("an.byType")));
  const types = new Map();
  for (const [path, mb] of stats.list) {
    const type = splitName(path.split("/").pop())[1].toUpperCase() || "-";
    types.set(type, (types.get(type) || 0) + mb * MiB);
  }
  const ranked = [...types].sort((a, b) => b[1] - a[1]);
  if (!ranked.length) win.body.append(node("div", "an-line", t("an.none")));
  for (const [type, bytes] of ranked) {
    const row = node("div", "an-row");
    const track = node("span", "an-track");
    const barEl = node("i");
    barEl.style.width = `${Math.max(2, (bytes / stats.bytes) * 100)}%`;
    track.append(barEl);
    row.append(node("span", "", type), track, node("span", "", fmtBytes(bytes)));
    win.body.append(row);
  }
  win.body.append(
    node("div", "an-line", `${t("an.duplicates")}: ${stats.dupes ? `${stats.dupes} (${fmtBytes(stats.dupeBytes)})` : t("an.none")}`),
    node("div", "an-line", `${t("an.empty")}: ${stats.empties || t("an.none")}`),
  );
  const [close] = footer(win, [[t("prog.close"), true]]);
  close.addEventListener("click", win.close);
};

const previewWin = (icon) => {
  const label = t("menu.preview");
  const pages = Number(icon.dataset.pages) || 1;
  const win = openWin(`Flect - ${label}`, true);
  const head = node("div", "pv-bar");
  const prev = node("button", "win-btn", "‹");
  const next = node("button", "win-btn", "›");
  const count = node("span", "pv-no");
  prev.type = "button";
  next.type = "button";
  head.append(node("span", "pv-name", icon.dataset.name), prev, count, next);
  const stage = node("div", "pv-stage");
  const page = node("div", "pv-page");
  stage.append(page);
  win.body.append(head, stage);
  let current = 1;
  const show = () => {
    count.textContent = `${current} / ${pages}`;
    prev.disabled = current === 1;
    next.disabled = current === pages;
    page.replaceChildren();
    for (let i = 0; i < 9; i++) {
      const line = node("i");
      line.style.width = `${i === 0 ? 58 : 62 + ((current * 7 + i * 13) % 36)}%`;
      page.append(line);
    }
  };
  prev.addEventListener("click", () => {
    current = Math.max(1, current - 1);
    show();
  });
  next.addEventListener("click", () => {
    current = Math.min(pages, current + 1);
    show();
  });
  show();
  const [close] = footer(win, [[t("prog.close"), true]]);
  close.addEventListener("click", win.close);
};

const countFiles = (n) => (n === 1 ? t("prog.file") : t("prog.files", { n }));
const countEmpty = (n) => (n === 1 ? t("prog.emptyFolder") : t("prog.emptyFolders", { n }));
const leaf = (label, run) => ({ label, run });
const group = (label, sub) => ({ label, sub });
const sized = (suffix, ext, size) => (icon) => [{ name: derive(icon.dataset.name, suffix, ext), bytes: size(bytesOf(icon)) }];
const scale = (weights) => (fmt) => [fmt, (bytes, from) => (bytes / (weights[from] || 1)) * weights[fmt]];

const flectMenu = (sel) => {
  const info = sel.map(classify);
  const cat = info[0].cat;
  if (cat === "none" || info.some((entry) => entry.cat !== cat)) return [];
  const formats = new Set(info.map((entry) => entry.fmt));
  const max = Math.max(...sel.map(bytesOf));
  const one = sel.length === 1;
  const go = (label, plan) => () => runTask(bare(label), sel, plan);
  const total = (field) => sel.reduce((sum, icon) => sum + statsOf(contentOf(icon))[field], 0);

  const resize = (pre) => {
    const label = t("menu.resize");
    const plan = (ratio) => go(label, { pre: [pre], work: "scan.resizing", out: sized("_resized", null, (b) => b * ratio) });
    return group(label, [leaf("50%", plan(0.27)), leaf("25%", plan(0.07)), leaf(t("menu.custom"), plan(0.5))]);
  };
  const rotate = () => {
    const label = t("menu.rotate");
    const plan = (ratio) => go(label, { work: "scan.rotating", out: sized("_rotated", null, (b) => b * ratio) });
    return group(label, [leaf("45°", plan(1.3)), leaf("90°", plan(1)), leaf("180°", plan(1))]);
  };
  const compress = (pre, presets, discord) => {
    const label = t("menu.compress");
    const plan = (cap) => go(label, { pre: [pre], work: "scan.compress", out: sized("_compressed", null, (b) => Math.min(b * 0.6, cap * 0.94)) });
    const list = [];
    if (discord && max > 20 * MiB) list.push(leaf(`Discord (${fmtBytes(20 * MiB)})`, plan(20 * MiB)));
    for (const mb of presets) if (max > mb * MiB) list.push(leaf(fmtBytes(mb * MiB), plan(mb * MiB)));
    list.push(leaf(t("menu.custom"), plan(Infinity)));
    return group(label, list);
  };
  const convert = (targets) => {
    if (!targets.length) return null;
    const label = t("menu.convert");
    return group(label, targets.map(([fmt, size]) => leaf(t("menu.to", { fmt }), go(label, {
      work: "scan.converting",
      out: (icon) => [{ name: derive(icon.dataset.name, "", fmt.toLowerCase()), bytes: size(bytesOf(icon), classify(icon).fmt) }],
    }))));
  };
  const subtitles = () => {
    const label = t("menu.subtitles");
    return leaf(label, go(label, { pre: ["scan.extractAudio"], work: "scan.transcribing", out: sized("", "srt", () => 14336), dur: 1500 }));
  };

  if (cat === "image") {
    const ocr = t("menu.ocr");
    const strip = t("menu.strip");
    const keyed = t("menu.chromakey");
    return [
      resize("scan.analyzingImage"),
      compress("scan.analyzingImage", [10, 5, 3, 1], false),
      rotate(),
      convert(IMAGE_FORMATS.filter((fmt) => !formats.has(fmt)).map(scale(IMAGE_WEIGHT))),
      leaf(ocr, go(ocr, { work: "scan.ocr", out: sized("_ocr", "txt", () => 1843) })),
      leaf(strip, go(strip, { work: "scan.strip", out: sized("_clean", null, (b) => b * 0.99) })),
      leaf(keyed, go(keyed, { work: "scan.keying", out: sized("_keyed", "png", (b) => b * 2.2), dur: 1300 })),
    ].filter(Boolean);
  }

  if (cat === "video") {
    const trim = t("menu.trim");
    const thumb = t("menu.thumb");
    const audio = t("menu.extractAudio");
    const list = [resize("scan.analyzingVideo")];
    if (one) {
      list.push(
        leaf(trim, go(trim, { work: "scan.trimming", out: sized("_trimmed", null, (b) => b * 0.3) })),
        leaf(thumb, go(thumb, { work: "scan.exportFrame", out: sized("_thumbnail", "png", () => 2.4 * MiB) })),
      );
    }
    list.push(
      subtitles(),
      compress("scan.analyzingVideo", [50, 25, 5], true),
      rotate(),
      convert([...VIDEO_FORMATS.filter((fmt) => !formats.has(fmt)).map(scale(VIDEO_WEIGHT)), ["GIF", (b) => Math.min(b * 0.04, 48 * MiB)]]),
      group(audio, AUDIO_FORMATS.map((fmt) => leaf(t("menu.to", { fmt }), go(audio, { work: "scan.extractAudio", out: sized("", fmt.toLowerCase(), (b) => b * 0.0046 * AUDIO_WEIGHT[fmt]) })))),
    );
    return list;
  }

  if (cat === "audio") return [convert(AUDIO_FORMATS.filter((fmt) => !formats.has(fmt)).map(scale(AUDIO_WEIGHT))), subtitles()].filter(Boolean);

  if (cat === "pdf") {
    const merge = t("menu.merge");
    const label = t("menu.convert");
    const pagesOf = (icon) => Number(icon.dataset.pages) || 1;
    const perPage = (ext, bytes) => (icon) => {
      const count = pagesOf(icon);
      const width = String(count).length;
      return Array.from({ length: count }, (_, i) => ({ name: derive(icon.dataset.name, count === 1 ? "" : `_p${String(i + 1).padStart(width, "0")}`, ext), bytes }));
    };
    const list = [];
    if (one) list.push(leaf(t("menu.preview"), () => previewWin(sel[0])));
    else {
      list.push(leaf(merge, go(merge, {
        work: "scan.merging",
        once: true,
        out: () => [{ name: derive(sel[0].dataset.name, "_merged", "pdf"), bytes: sel.reduce((sum, icon) => sum + bytesOf(icon), 0) * 0.97, pages: sel.reduce((sum, icon) => sum + pagesOf(icon), 0) }],
      })));
    }
    list.push(
      leaf(t("menu.to", { fmt: "PNG" }), go(label, { work: "scan.converting", out: perPage("png", 820 * 1024) })),
      leaf(t("menu.to", { fmt: "JPG" }), go(label, { work: "scan.converting", out: perPage("jpg", 310 * 1024) })),
      leaf(t("menu.to", { fmt: "TXT" }), go(label, { work: "scan.converting", out: (icon) => [{ name: derive(icon.dataset.name, "", "txt"), bytes: pagesOf(icon) * 3100 }] })),
    );
    return list;
  }

  if (cat === "folder") {
    const dup = t("menu.dup");
    const empty = t("menu.empty");
    const tree = t("menu.tree");
    const rename = t("menu.rename");
    const analyze = t("menu.analyze");
    const pack = t("menu.compress");
    return [
      leaf(dup, go(dup, {
        pre: ["scan.scanFolder", "scan.compareHash"],
        work: "scan.moveBin",
        summary: () => (total("dupes") ? t("prog.recycled", { count: countFiles(total("dupes")), size: fmtBytes(total("dupeBytes")) }) : t("prog.noDupes")),
        apply: () => sel.forEach((icon) => contents.set(icon, contentOf(icon).filter((entry) => !entry[2]))),
      })),
      leaf(empty, go(empty, {
        pre: ["scan.lookingEmpty"],
        work: "scan.moveBin",
        summary: () => (total("empties") ? t("prog.emptyRecycled", { count: countEmpty(total("empties")) }) : t("prog.noEmpty")),
        apply: () => sel.forEach((icon) => contents.set(icon, contentOf(icon).filter(([path]) => !path.endsWith("/")))),
      })),
      leaf(tree, go(tree, { work: "scan.tree", present: () => treeWin(sel) })),
      leaf(rename, go(rename, { work: "scan.renaming", summary: () => t("prog.renamed", { count: countFiles(total("files")) }) })),
      leaf(analyze, go(analyze, { work: "scan.analyzing", present: () => analyzeWin(sel) })),
      leaf(pack, go(pack, {
        work: "scan.compressFolder",
        out: (icon) => [{ name: derive(icon.dataset.name, "", "zip"), bytes: Math.max(22, statsOf(contentOf(icon)).bytes * 0.93), content: contentOf(icon) }],
        dur: 1200,
      })),
    ];
  }

  if (cat === "archive") {
    const label = t("menu.extract");
    return [leaf(label, go(label, { work: "scan.extracting", out: (icon) => [{ name: archiveStem(icon.dataset.name), folder: true, content: contentOf(icon) }], dur: 1200 }))];
  }

  if (cat === "jar") {
    const label = t("menu.decompile");
    return [leaf(label, go(label, { work: "scan.decompiling", out: (icon) => [{ name: splitName(icon.dataset.name)[0], folder: true }], dur: 1400 }))];
  }

  const list = [];
  if (formats.size === 1) {
    const label = t("menu.convert");
    const targets = textTargets([...formats][0]);
    if (targets.length) {
      list.push(group(label, targets.map((fmt) => leaf(t("menu.to", { fmt }), go(label, { work: "scan.converting", out: sized("", fmt.toLowerCase(), (b) => b * 1.15) })))));
    }
  }
  const encoding = t("menu.encoding");
  const endings = t("menu.lineEndings");
  list.push(
    group(encoding, [["to UTF-8", "_utf8", 1], ["to UTF-8 (BOM)", "_utf8bom", 1], ["to UTF-16", "_utf16", 2], ["to Shift_JIS", "_sjis", 0.8]]
      .map(([label, suffix, ratio]) => leaf(label, go(encoding, { work: "scan.encoding", out: sized(suffix, null, (b) => b * ratio) })))),
    group(endings, [["to CRLF (Windows)", "_crlf", 1.03], ["to LF (Unix)", "_lf", 0.97]]
      .map(([label, suffix, ratio]) => leaf(label, go(endings, { work: "scan.writing", out: sized(suffix, null, (b) => b * ratio) })))),
  );
  if (formats.size === 1 && formats.has("JSON")) {
    const pretty = t("menu.pretty");
    const keys = t("menu.sortKeys");
    list.push(
      leaf(pretty, go(pretty, { work: "scan.writing", out: sized("_pretty", null, (b) => b * 1.4) })),
      leaf(keys, go(keys, { work: "scan.writing", out: sized("_sorted", null, (b) => b) })),
    );
  }
  return list;
};

let renaming = null;
let renameTimer = 0;
let renameArmed = null;
let balloon = null;
let balloonTimer = 0;

const hideBalloon = () => {
  clearTimeout(balloonTimer);
  balloon?.remove();
  balloon = null;
};

const showBalloon = (target) => {
  hideBalloon();
  balloon = node("div", "balloon");
  balloon.setAttribute("role", "alert");
  balloon.append(node("span", "", t("std.badChars")), node("b", "", '\\ / : * ? " < > |'));
  iconsEl.append(balloon);
  const box = iconsEl.getBoundingClientRect();
  const at = target.getBoundingClientRect();
  balloon.style.left = `${clamp(at.left - box.left, 4, Math.max(4, iconsEl.clientWidth - balloon.offsetWidth - 4))}px`;
  balloon.style.top = `${at.bottom - box.top + 6}px`;
  balloonTimer = setTimeout(hideBalloon, 3200);
};

const finishRename = (commit) => {
  if (!renaming) return;
  const { icon, box } = renaming;
  renaming = null;
  const value = box.value.trim().replace(/[. ]+$/, "");
  box.remove();
  hideBalloon();
  icon.classList.remove("renaming");
  if (commit && value && value !== icon.dataset.name) {
    const [stem, ext] = icon.dataset.kind === "folder" ? [value, ""] : splitName(value);
    setName(icon, uniqueName(stem, ext, icon));
  }
  focusIcon(icon);
};

const startRename = (icon) => {
  if (!icon?.isConnected || icon.classList.contains("gone") || icon.classList.contains("writing")) return;
  if (renaming) finishRename(true);
  closeMenus();
  clearTimeout(renameTimer);
  renameArmed = null;
  select([icon]);
  anchor = icon;
  focusIcon(icon);
  const nm = icon.querySelector(".nm");
  const box = node("textarea", "rename-box");
  box.value = icon.dataset.name;
  box.rows = 1;
  box.spellcheck = false;
  box.setAttribute("aria-label", t("std.rename"));
  icon.classList.add("renaming");
  iconsEl.append(box);
  const area = iconsEl.getBoundingClientRect();
  const tile = icon.getBoundingClientRect();
  const label = nm.getBoundingClientRect();
  if (desk.dataset.view === "small") {
    box.classList.add("left");
    box.style.left = `${label.left - area.left - 3}px`;
    box.style.width = `${Math.max(tile.right - label.left + 40, 140)}px`;
  } else {
    box.style.left = `${tile.left - area.left - 10}px`;
    box.style.width = `${tile.width + 20}px`;
  }
  box.style.top = `${label.top - area.top - 2}px`;
  const fit = () => {
    box.style.height = "auto";
    box.style.height = `${box.scrollHeight + 2}px`;
  };
  fit();
  renaming = { icon, box };
  box.focus({ preventScroll: true });
  const [stem, ext] = splitName(box.value);
  box.setSelectionRange(0, icon.dataset.kind === "folder" || !ext ? box.value.length : stem.length);
  box.addEventListener("keydown", (e) => {
    e.stopPropagation();
    if (e.key === "Enter") {
      e.preventDefault();
      finishRename(true);
    } else if (e.key === "Escape") {
      e.preventDefault();
      finishRename(false);
    }
  });
  box.addEventListener("input", () => {
    const clean = box.value.replace(STRIP_NAME, "");
    if (clean !== box.value) {
      const warn = BAD_NAME.test(box.value);
      const caret = box.value.slice(0, box.selectionStart).replace(STRIP_NAME, "").length;
      box.value = clean;
      box.setSelectionRange(caret, caret);
      if (warn) showBalloon(box);
    }
    fit();
  });
  box.addEventListener("blur", () => {
    if (renaming?.box === box) finishRename(true);
  });
};

const sortIcons = (key) => {
  const collator = new Intl.Collator(document.documentElement.lang, { numeric: true, sensitivity: "base" });
  const typeOf = (icon) => (icon.dataset.kind === "folder" ? "" : splitName(icon.dataset.name)[1]);
  const compare = {
    name: (a, b) => collator.compare(a.dataset.name, b.dataset.name),
    size: (a, b) => bytesOf(a) - bytesOf(b),
    type: (a, b) => collator.compare(typeOf(a), typeOf(b)) || collator.compare(a.dataset.name, b.dataset.name),
    date: (a, b) => Number(b.dataset.modified) - Number(a.dataset.modified),
  }[key];
  const folderFirst = (a, b) => (b.dataset.kind === "folder") - (a.dataset.kind === "folder");
  const active = document.activeElement;
  iconsEl.append(...items().sort((a, b) => folderFirst(a, b) || compare(a, b)));
  if (active?.classList.contains("icon") && active.isConnected) active.focus({ preventScroll: true });
};

const refreshDesk = () => {
  iconsEl.classList.add("refreshing");
  setTimeout(() => iconsEl.classList.remove("refreshing"), 160);
};

const createNew = (folder) => {
  const name = folder ? uniqueName(t("std.folderName"), "") : uniqueName(t("std.textName"), "txt");
  const icon = makeIcon({ name, folder });
  icon.classList.remove("born");
  startRename(icon);
};

const itemMenu = (sel) => {
  const flect = flectMenu(sel);
  const list = [{ label: t("std.open"), bold: true, off: true }, SEP];
  if (flect.length) list.push({ label: "Flect", img: "assets/Flect.png", sub: flect }, SEP);
  list.push(
    { label: t("std.cut"), off: true },
    { label: t("std.copy"), off: true },
    SEP,
    { label: t("std.delete"), off: true },
    { label: t("std.rename"), off: sel.length !== 1, run: () => startRename(sel[0]) },
    SEP,
    { label: t("std.properties"), off: true },
  );
  return list;
};

const deskMenu = () => {
  const view = desk.dataset.view || "large";
  const views = [["large", t("std.large")], ["medium", t("std.medium")], ["small", t("std.small")]];
  const sorts = [["name", t("std.sortName")], ["size", t("std.sortSize")], ["type", t("std.sortType")], ["date", t("std.sortDate")]];
  return [
    { label: t("std.view"), sub: views.map(([id, label]) => ({ label, radio: view === id, run: () => { desk.dataset.view = id; } })) },
    { label: t("std.sort"), sub: sorts.map(([id, label]) => leaf(label, () => sortIcons(id))) },
    { label: t("std.refresh"), run: refreshDesk },
    SEP,
    { label: t("std.paste"), off: true },
    SEP,
    { label: t("std.new"), sub: [{ label: t("std.newFolder"), svg: MINI.folder, run: () => createNew(true) }, SEP, { label: t("std.newText"), svg: MINI.text, run: () => createNew(false) }] },
    SEP,
    { label: t("desk.about"), run: () => openModal() },
  ];
};

const toArea = (e) => {
  const box = iconsEl.getBoundingClientRect();
  return { x: e.clientX - box.left, y: e.clientY - box.top };
};

const areaRect = (el) => {
  const box = iconsEl.getBoundingClientRect();
  const r = el.getBoundingClientRect();
  return { left: r.left - box.left, top: r.top - box.top, right: r.right - box.left, bottom: r.bottom - box.top };
};

const startMarquee = (e, ctrl) => {
  const base = new Set(ctrl ? selected() : []);
  if (!ctrl) select([]);
  const origin = toArea(e);
  let frame = null;
  const move = (ev) => {
    const p = toArea(ev);
    if (!frame) {
      if (Math.abs(p.x - origin.x) < 4 && Math.abs(p.y - origin.y) < 4) return;
      frame = node("div", "marquee");
      iconsEl.append(frame);
    }
    const x = clamp(p.x, 0, iconsEl.clientWidth);
    const y = clamp(p.y, 0, iconsEl.clientHeight);
    const left = Math.min(origin.x, x);
    const top = Math.min(origin.y, y);
    const right = Math.max(origin.x, x);
    const bottom = Math.max(origin.y, y);
    Object.assign(frame.style, { left: `${left}px`, top: `${top}px`, width: `${right - left}px`, height: `${bottom - top}px` });
    select(items().filter((icon) => {
      const r = areaRect(icon);
      return base.has(icon) !== (r.left < right && r.right > left && r.top < bottom && r.bottom > top);
    }));
  };
  const end = () => {
    iconsEl.removeEventListener("pointermove", move);
    iconsEl.removeEventListener("pointerup", end);
    iconsEl.removeEventListener("pointercancel", end);
    frame?.remove();
  };
  iconsEl.setPointerCapture(e.pointerId);
  iconsEl.addEventListener("pointermove", move);
  iconsEl.addEventListener("pointerup", end);
  iconsEl.addEventListener("pointercancel", end);
};

iconsEl.addEventListener("pointerdown", (e) => {
  if (e.target.closest(".rename-box")) return;
  clearTimeout(renameTimer);
  renameArmed = null;
  if (renaming) finishRename(true);
  const icon = e.target.closest(".icon");
  const ctrl = e.ctrlKey || e.metaKey;
  if (!icon) {
    if (e.button === 2) select([]);
    else if (e.button === 0 && e.pointerType === "mouse") startMarquee(e, ctrl);
    else if (e.button === 0 && !ctrl) select([]);
    iconsEl.focus({ preventScroll: true });
    return;
  }
  if (e.button === 2) {
    if (!icon.classList.contains("selected")) {
      select([icon]);
      anchor = icon;
    }
    focusIcon(icon);
    return;
  }
  if (e.button !== 0) return;
  const sole = icon.classList.contains("selected") && selected().length === 1;
  if (e.shiftKey) {
    const all = items();
    const from = all.indexOf(anchor?.isConnected ? anchor : icon);
    const to = all.indexOf(icon);
    const range = all.slice(Math.min(from, to), Math.max(from, to) + 1);
    select(ctrl ? [...selected(), ...range] : range);
  } else if (ctrl) {
    select(icon.classList.contains("selected") ? selected().filter((other) => other !== icon) : [...selected(), icon]);
    anchor = icon;
  } else {
    select([icon]);
    anchor = icon;
    if (sole && e.target.closest(".nm")) renameArmed = icon;
  }
  focusIcon(icon);
});

iconsEl.addEventListener("click", (e) => {
  const icon = e.target.closest(".icon");
  if (!icon || icon !== renameArmed) return;
  renameTimer = setTimeout(() => {
    if (renameArmed === icon) startRename(icon);
  }, 500);
});

iconsEl.addEventListener("dblclick", () => {
  clearTimeout(renameTimer);
  renameArmed = null;
});

let typed = "";
let typedAt = 0;

iconsEl.addEventListener("keydown", (e) => {
  if (renaming || e.target.closest(".rename-box")) return;
  const all = items();
  if (!all.length) return;
  const current = e.target.closest(".icon");
  const index = current ? all.indexOf(current) : -1;
  const ctrl = e.ctrlKey || e.metaKey;
  const columns = () => {
    const top = all[0].offsetTop;
    const count = all.findIndex((icon) => icon.offsetTop !== top);
    return count < 0 ? all.length : Math.max(1, count);
  };
  const go = (target) => {
    const next = all[clamp(target, 0, all.length - 1)];
    if (e.shiftKey) {
      const from = all.indexOf(anchor?.isConnected ? anchor : next);
      const to = all.indexOf(next);
      select(all.slice(Math.min(from, to), Math.max(from, to) + 1));
    } else if (!ctrl) {
      select([next]);
      anchor = next;
    }
    focusIcon(next, true);
  };
  if (e.key === "ArrowRight") go(index + 1);
  else if (e.key === "ArrowLeft") go(index < 0 ? 0 : index - 1);
  else if (e.key === "ArrowDown") go(index < 0 ? 0 : index + columns());
  else if (e.key === "ArrowUp") go(index < 0 ? 0 : index - columns());
  else if (e.key === "Home") go(0);
  else if (e.key === "End") go(all.length - 1);
  else if (e.key === "F2") {
    if (current) startRename(current);
  } else if (e.key === "ContextMenu" || (e.shiftKey && e.key === "F10")) openContext(current || iconsEl, 0, 0);
  else if (e.key === " " && current) {
    if (ctrl) select(current.classList.contains("selected") ? selected().filter((icon) => icon !== current) : [...selected(), current]);
    else select([current]);
    anchor = current;
  } else if (ctrl && e.key.toLowerCase() === "a") select(all);
  else if (e.key.length === 1 && !ctrl && !e.altKey) {
    const now = performance.now();
    typed = now - typedAt > 900 ? e.key : typed + e.key;
    typedAt = now;
    const query = typed.toLowerCase();
    const start = typed.length === 1 ? index + 1 : Math.max(index, 0);
    const hit = [...all.slice(start), ...all.slice(0, start)].find((icon) => icon.dataset.name.toLowerCase().startsWith(query));
    if (!hit) return;
    select([hit]);
    anchor = hit;
    focusIcon(hit, true);
  } else return;
  e.preventDefault();
});

const openContext = (target, clientX, clientY) => {
  if (renaming) finishRename(true);
  const icon = target.closest(".icon");
  if (icon && !icon.classList.contains("selected")) {
    select([icon]);
    anchor = icon;
    focusIcon(icon);
  }
  const sel = selected();
  let x = clientX;
  let y = clientY;
  if (sel.length && (icon || target === iconsEl)) {
    if (!x && !y) {
      const r = (icon || sel[0]).getBoundingClientRect();
      x = r.left + r.width / 2;
      y = r.top + r.height / 2;
    }
    openMenu(itemMenu(sel), x, y);
    return;
  }
  select([]);
  if (!x && !y) {
    const r = iconsEl.getBoundingClientRect();
    x = r.left + 24;
    y = r.top + 24;
  }
  openMenu(deskMenu(), x, y);
};

desk.addEventListener("contextmenu", (e) => {
  if (e.target.closest(".rename-box")) return;
  e.preventDefault();
  openContext(e.target, e.clientX, e.clientY);
});

for (const [index, icon] of items().entries()) {
  const id = icon.dataset.content;
  if (id && Object.hasOwn(CONTENT, id)) contents.set(icon, CONTENT[id].map((entry) => [...entry]));
  else if (icon.dataset.kind === "folder") contents.set(icon, []);
  icon.dataset.modified = String(Date.UTC(2025, 8, 1 + index, 9));
}
desk.dataset.view = "large";

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
  const el = modalEl;
  modalEl = null;
  el.classList.remove("open");
  setTimeout(() => el.remove(), 220);
};

$("#year").textContent = new Date().getFullYear();

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

for (const btn of $$(".copy-btn")) {
  let timer = 0;
  btn.addEventListener("click", async () => {
    const source = document.getElementById(btn.dataset.copyTarget);
    const label = btn.querySelector("span");
    if (!source || !label) return;
    let key = "install.ps.copied";
    try {
      await navigator.clipboard.writeText(source.textContent.trim());
    } catch {
      const range = document.createRange();
      range.selectNodeContents(source);
      const selection = getSelection();
      selection.removeAllRanges();
      selection.addRange(range);
      key = "install.ps.copyManual";
    }
    label.textContent = t(key);
    btn.classList.add("done");
    clearTimeout(timer);
    timer = setTimeout(() => {
      label.textContent = t("install.ps.copy");
      btn.classList.remove("done");
    }, 1800);
  });
}

const hvTabsWrap = $("#heroPreviewTabs");
if (hvTabsWrap) {
  const hvBadge = $("#hvBadge");
  const hvFileName = $("#hvFileName");
  const hvFileSize = $("#hvFileSize");
  const hvSubMenu = $("#hvSubMenu");
  const hvOutCode = $("#hvOutCode");
  const hvButtons = $$("[data-hv]", hvTabsWrap);
  const hvOrder = ["image", "video", "folder", "pdf"];
  let hvCurrent = "image";
  let hvTimer = 0;

  const hvConfigs = {
    image: () => ({
      badge: "PNG",
      name: "Key_Visual_4K.png",
      size: "3840 × 2160 · 12.4 MB",
      out: "Key_Visual_4K.webp · 1.1 MB (-91%)",
      items: [
        [t("menu.resize"), true, false],
        [t("menu.compress"), true, false],
        [t("menu.convert"), true, true],
        [t("menu.ocr"), false, false],
        [t("menu.strip"), false, false],
        [t("menu.chromakey"), false, false],
      ],
    }),
    video: () => ({
      badge: "MP4",
      name: "Showcase_Reel.mp4",
      size: "60 fps · 420 MB",
      out: "Showcase_Reel_24MB.mp4 · 23.8 MB (-94%)",
      items: [
        [t("menu.resize"), true, false],
        [t("menu.compress"), true, true],
        [t("menu.trim"), false, false],
        [t("menu.convert"), true, false],
        [t("menu.subtitles"), true, false],
        [t("menu.extractAudio"), true, false],
      ],
    }),
    folder: () => ({
      badge: "DIR",
      name: "Raw_Photos_2026",
      size: "418 items · 3.2 GB",
      out: "34 duplicates & 12 empty folders removed",
      items: [
        [t("menu.dup"), false, true],
        [t("menu.empty"), false, false],
        [t("menu.tree"), false, false],
        [t("menu.rename"), false, false],
        [t("menu.analyze"), false, false],
        [t("menu.compress"), true, false],
      ],
    }),
    pdf: () => ({
      badge: "PDF",
      name: "Architecture_Spec.pdf",
      size: "48 pages · 18.6 MB",
      out: "Architecture_Spec_p01-48.png · 48 pages exported",
      items: [
        [t("menu.convert"), true, true],
        [t("menu.merge"), false, false],
        [t("menu.compress"), true, false],
        [t("menu.ocr"), false, false],
      ],
    }),
  };

  const renderHv = (kind) => {
    hvCurrent = kind;
    const cfg = (hvConfigs[kind] || hvConfigs.image)();
    for (const b of hvButtons) b.classList.toggle("active", b.dataset.hv === kind);
    if (hvBadge) hvBadge.textContent = cfg.badge;
    if (hvFileName) hvFileName.textContent = cfg.name;
    if (hvFileSize) hvFileSize.textContent = cfg.size;
    if (hvOutCode) hvOutCode.textContent = cfg.out;
    if (hvSubMenu) {
      hvSubMenu.replaceChildren(...cfg.items.map(([label, hasSub, isAct]) => {
        const row = document.createElement("div");
        row.className = `hv-row${isAct ? " active-sub" : ""}`;
        const span = document.createElement("span");
        span.textContent = label;
        row.append(span);
        if (hasSub) {
          const chev = document.createElement("i");
          chev.className = "hv-chev";
          chev.textContent = "›";
          row.append(chev);
        }
        return row;
      }));
    }
  };

  const startHvCycle = () => {
    clearInterval(hvTimer);
    hvTimer = setInterval(() => {
      const nextIdx = (hvOrder.indexOf(hvCurrent) + 1) % hvOrder.length;
      renderHv(hvOrder[nextIdx]);
    }, 1500);
  };

  for (const btn of hvButtons) {
    btn.addEventListener("click", () => {
      renderHv(btn.dataset.hv);
      startHvCycle();
    });
    btn.addEventListener("pointerenter", () => {
      renderHv(btn.dataset.hv);
      clearInterval(hvTimer);
    });
  }

  const hvShell = hvTabsWrap.closest(".hv-shell");
  if (hvShell) {
    hvShell.addEventListener("pointerenter", () => clearInterval(hvTimer));
    hvShell.addEventListener("pointerleave", startHvCycle);
  }

  addEventListener("langchange", () => renderHv(hvCurrent));
  renderHv(hvCurrent);
  startHvCycle();
}

(() => {
  const heroTitle = $(".hero h1");
  if (!heroTitle) return;
  let slotTimer = 0;
  let wordIndex = 0;
  let paused = false;

  const getWords = () => {
    const list = t("grad.words");
    return Array.isArray(list) && list.length ? list : ["Everything"];
  };

  const setupSlot = () => {
    const grad = $(".grad", heroTitle);
    if (!grad) return null;
    const words = getWords();
    wordIndex = 0;
    grad.classList.add("slot-ready");
    const item = document.createElement("span");
    item.className = "grad-item";
    item.textContent = words[0];
    grad.replaceChildren(item);
    return grad;
  };

  const stepWord = () => {
    if (paused) return;
    const grad = $(".grad", heroTitle);
    if (!grad) return;
    const words = getWords();
    if (words.length < 2) return;
    wordIndex = (wordIndex + 1) % words.length;
    const current = $(".grad-item:not(.slide-out)", grad);
    const incoming = document.createElement("span");
    incoming.className = "grad-item slide-in";
    incoming.textContent = words[wordIndex];
    if (current) {
      current.classList.remove("slide-in");
      current.classList.add("slide-out");
      setTimeout(() => current.remove(), 360);
    }
    grad.append(incoming);
  };

  const startSlot = () => {
    clearInterval(slotTimer);
    slotTimer = setInterval(stepWord, 1500);
  };

  heroTitle.addEventListener("pointerenter", () => {
    paused = true;
  });
  heroTitle.addEventListener("pointerleave", () => {
    paused = false;
  });

  addEventListener("langchange", () => {
    setupSlot();
    startSlot();
  });

  setupSlot();
  startSlot();
})();

(() => {
  const starsEl = $("#statStars");
  const dlEl = $("#statDownloads");
  if (!starsEl && !dlEl) return;

  const fmtNum = (n) => new Intl.NumberFormat().format(n);
  const applyStats = (stars, downloads) => {
    if (starsEl && Number.isFinite(stars) && stars > 0) starsEl.textContent = fmtNum(stars);
    if (dlEl && Number.isFinite(downloads) && downloads > 0) dlEl.textContent = fmtNum(downloads);
  };

  try {
    const cached = JSON.parse(sessionStorage.getItem("flect-gh-stats") || "null");
    if (cached && cached.ts && Date.now() - cached.ts < 600000) {
      applyStats(cached.stars, cached.downloads);
      return;
    }
  } catch {}

  Promise.all([
    fetch("https://api.github.com/repos/Konayukiw/Flect").then((r) => (r.ok ? r.json() : null)),
    fetch("https://api.github.com/repos/Konayukiw/Flect/releases?per_page=100").then((r) => (r.ok ? r.json() : null)),
  ]).then(([repo, releases]) => {
    const stars = repo && typeof repo.stargazers_count === "number" ? repo.stargazers_count : 0;
    const downloads = Array.isArray(releases)
      ? releases.reduce((sum, rel) => sum + (rel.assets || []).reduce((a, b) => a + (b.download_count || 0), 0), 0)
      : 0;
    if (stars > 0 || downloads > 0) {
      applyStats(stars, downloads);
      try {
        sessionStorage.setItem("flect-gh-stats", JSON.stringify({ stars, downloads, ts: Date.now() }));
      } catch {}
    }
  }).catch(() => {});
})();

const installTabsWrap = $("#installTabs");
if (installTabsWrap) {
  const tabBtns = $$("[data-install-tab]", installTabsWrap);
  const panels = $$("[data-install-panel]");
  for (const btn of tabBtns) {
    btn.addEventListener("click", () => {
      const target = btn.dataset.installTab;
      for (const b of tabBtns) {
        const active = b === btn;
        b.classList.toggle("active", active);
        b.setAttribute("aria-selected", String(active));
      }
      for (const panel of panels) {
        panel.classList.toggle("hidden-panel", panel.dataset.installPanel !== target);
      }
    });
  }
}

const featTabsWrap = $("#featTabs");
const featBento = $("#featBento");
if (featTabsWrap && featBento) {
  const tabBtns = $$("[data-feat-tab]", featTabsWrap);
  const cards = $$("[data-feat-cat]", featBento);
  for (const btn of tabBtns) {
    btn.addEventListener("click", () => {
      const cat = btn.dataset.featTab;
      for (const b of tabBtns) b.classList.toggle("active", b === btn);
      featBento.classList.toggle("single-col", cat !== "all");
      for (const card of cards) {
        const show = cat === "all" || card.dataset.featCat === cat;
        card.classList.toggle("hidden-cat", !show);
      }
    });
  }
}

const ambientCanvas = $("#ambientCanvas");
if (ambientCanvas) {
  const ctx = ambientCanvas.getContext("2d");
  const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)");
  let width = 0;
  let height = 0;
  let pointerX = 0.5;
  let pointerY = 0.25;
  let smoothX = 0.5;
  let smoothY = 0.25;

  const resizeCanvas = () => {
    const dpr = Math.min(devicePixelRatio || 1, 1.5);
    width = innerWidth;
    height = innerHeight;
    ambientCanvas.width = Math.round(width * dpr);
    ambientCanvas.height = Math.round(height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  };

  addEventListener("resize", resizeCanvas);
  addEventListener("pointermove", (e) => {
    pointerX = e.clientX / Math.max(1, width);
    pointerY = e.clientY / Math.max(1, height);
  }, { passive: true });
  resizeCanvas();

  const drawAmbient = (time) => {
    const isLight = document.documentElement.dataset.theme === "light";
    smoothX += (pointerX - smoothX) * 0.05;
    smoothY += (pointerY - smoothY) * 0.05;
    ctx.clearRect(0, 0, width, height);

    const tSec = time * 0.00035;
    const orbs = [
      {
        x: width * (0.22 + Math.cos(tSec) * 0.08),
        y: height * (0.18 + Math.sin(tSec * 1.2) * 0.06),
        r: Math.max(width, height) * 0.42,
        c: isLight ? "rgba(0, 148, 212, 0.10)" : "rgba(0, 200, 255, 0.11)",
      },
      {
        x: width * (0.78 + Math.sin(tSec * 0.9) * 0.07),
        y: height * (0.32 + Math.cos(tSec * 1.1) * 0.07),
        r: Math.max(width, height) * 0.38,
        c: isLight ? "rgba(0, 168, 120, 0.08)" : "rgba(0, 229, 163, 0.08)",
      },
      {
        x: width * smoothX,
        y: height * smoothY,
        r: Math.max(width, height) * 0.28,
        c: isLight ? "rgba(0, 160, 220, 0.07)" : "rgba(46, 216, 255, 0.075)",
      },
    ];

    for (const orb of orbs) {
      const grad = ctx.createRadialGradient(orb.x, orb.y, 0, orb.x, orb.y, orb.r);
      grad.addColorStop(0, orb.c);
      grad.addColorStop(1, "rgba(0, 0, 0, 0)");
      ctx.fillStyle = grad;
      ctx.fillRect(0, 0, width, height);
    }

    if (!reducedMotion.matches) requestAnimationFrame(drawAmbient);
  };

  requestAnimationFrame(drawAmbient);
}
