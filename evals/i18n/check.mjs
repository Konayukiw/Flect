import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, extname, sep } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const root = fileURLToPath(new URL("../..", import.meta.url));
const annotate = process.env.GITHUB_ACTIONS === "true";
const errors = [];
const notes = [];

const report = (list, message, file, line) => list.push({ message, file, line });
const lineOf = (text, index) => text.slice(0, index).split("\n").length;
const shown = (file) => relative(root, file).split(sep).join("/");

const walk = (directory, extensions) =>
  readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) {
      return ["bin", "obj", "node_modules"].includes(name) ? [] : walk(path, extensions);
    }
    return extensions.includes(extname(name)) ? [path] : [];
  });

const keyPattern = /^[a-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$/;
const fileExtensions = new Set(["json", "txt", "xml", "csv", "html", "md", "png", "jpg", "exe", "dll", "py", "ini", "log"]);
const webFileExtensions = new Set([...fileExtensions, "js", "mjs", "css", "svg", "gif", "webp", "mp4", "webm", "ico", "pdf", "zip"]);

function checkApp() {
  const folder = join(root, "src", "App", "Main", "I18n");
  const files = { en: "Strings.En.cs", ja: "Strings.Ja.cs", zh: "Strings.Zh.cs" };
  const tables = {};

  for (const [language, name] of Object.entries(files)) {
    const path = join(folder, name);
    const text = readFileSync(path, "utf8");
    const keys = new Map();
    for (const match of text.matchAll(/^\s*\["([^"]+)"\]\s*=/gm)) {
      const line = lineOf(text, match.index);
      if (keys.has(match[1])) report(errors, `App: "${match[1]}" is defined twice in ${language}`, path, line);
      keys.set(match[1], line);
    }
    tables[language] = { path, keys };
  }

  const english = tables.en.keys;
  const everyKey = new Set(Object.values(tables).flatMap((table) => [...table.keys.keys()]));
  for (const key of everyKey) {
    if (key.endsWith(".one")) {
      const base = key.slice(0, -".one".length);
      if (!english.has(base)) {
        report(errors, `App: Plural form "${key}" has no base key "${base}"`, tables.en.path, english.get(key));
      }
      continue;
    }
    for (const [language, table] of Object.entries(tables)) {
      if (!table.keys.has(key)) {
        const owner = Object.values(tables).find((other) => other.keys.has(key));
        report(errors, `App: "${key}" is missing in ${language}`, owner.path, owner.keys.get(key));
      }
    }
  }

  const namespaces = new Set([...english.keys()].map((key) => key.split(".")[0]));
  const used = new Map();
  const optional = new Map();
  const use = (key, file, line) => {
    if (!used.has(key)) used.set(key, { file, line });
  };

  for (const file of walk(join(root, "src", "App"), [".cs", ".xaml"])) {
    if (file.startsWith(folder)) continue;
    const text = readFileSync(file, "utf8");

    if (file.endsWith(".xaml")) {
      for (const match of text.matchAll(/\{(?:loc:)?T\s+(?:Key=)?([A-Za-z0-9_.]+)\s*\}/g)) {
        use(match[1], file, lineOf(text, match.index));
      }
      continue;
    }

    for (const match of text.matchAll(/Loc\.[TFN]\(/g)) {
      const start = match.index + match[0].length;
      for (const literal of firstArgumentLiterals(text, start)) {
        if (keyPattern.test(literal)) use(literal, file, lineOf(text, start));
      }
    }

    for (const match of text.matchAll(/MenuEntry\.(?:Group|Leaf)\(\s*"([^"]+)"\s*,\s*"[^"]*"\s*(?:,\s*"([^"]+)")?/g)) {
      const line = lineOf(text, match.index);
      if (match[2]) {
        if (!optional.has(match[2])) optional.set(match[2], { file, line });
      } else {
        use(`menu.${match[1]}`, file, line);
      }
    }

    for (const match of text.matchAll(/\(\s*[A-Z][A-Za-z0-9_]*\.[A-Za-z0-9_]+\s*,\s*"([a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)"\s*\)/g)) {
      use(match[1], file, lineOf(text, match.index));
    }

    for (const match of text.matchAll(/"([a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)"/g)) {
      const literal = match[1];
      const segments = literal.split(".");
      if (!namespaces.has(segments[0]) || optional.has(literal)) continue;
      if (fileExtensions.has(segments.at(-1).toLowerCase())) continue;
      use(literal, file, lineOf(text, match.index));
    }
  }

  for (const [key, where] of used) {
    if (!english.has(key)) report(errors, `App: "${key}" is used but not defined`, where.file, where.line);
  }
  for (const [key, where] of optional) {
    if (!english.has(key) && !used.has(key)) {
      report(notes, `App: Menu label key "${key}" is not defined, so the English label is shown`, where.file, where.line);
    }
  }
  for (const [key, line] of english) {
    if (!key.endsWith(".one") && !used.has(key) && !optional.has(key)) {
      report(notes, `App: "${key}" looks unused`, tables.en.path, line);
    }
  }
}

function firstArgumentLiterals(text, start) {
  const literals = [];
  let depth = 0;
  for (let index = start; index < text.length; index++) {
    const character = text[index];
    if (character === '"') {
      let end = index + 1;
      while (end < text.length && text[end] !== '"') end += text[end] === "\\" ? 2 : 1;
      literals.push(text.slice(index + 1, end));
      index = end;
    } else if (character === "(" || character === "[" || character === "{") {
      depth++;
    } else if (character === ")" || character === "]" || character === "}") {
      if (depth === 0) break;
      depth--;
    } else if (character === "," && depth === 0) {
      break;
    }
  }
  return literals;
}

function checkWeb() {
  const web = join(root, "src", "Web");
  const path = join(web, "js", "i18n.js");
  const source = readFileSync(path, "utf8");
  const marker = "const I18N = ";
  const start = source.indexOf(marker) + marker.length;
  const end = source.indexOf("\nlet currentLang");
  if (start < marker.length || end < 0) {
    report(errors, "Web: Could not locate the I18n table", path, 1);
    return;
  }

  const table = vm.runInNewContext(`(${source.slice(start, end).trim().replace(/;$/, "")})`);
  const languages = Object.keys(table);

  const blocks = [...source.slice(0, end).matchAll(/^ {2}([a-z]{2}(?:-[A-Za-z]+)?): \{/gm)];
  blocks.forEach((block, position) => {
    const from = block.index;
    const to = position + 1 < blocks.length ? blocks[position + 1].index : end;
    const seen = new Set();
    for (const match of source.slice(from, to).matchAll(/^ {4}"([^"]+)"\s*:/gm)) {
      const line = lineOf(source, from + match.index);
      if (seen.has(match[1])) report(errors, `Web: "${match[1]}" is defined twice in ${block[1]}`, path, line);
      seen.add(match[1]);
    }
  });

  const lineOfKey = (key) => {
    const index = source.indexOf(`"${key}"`);
    return index < 0 ? 1 : lineOf(source, index);
  };

  const everyKey = new Set(languages.flatMap((language) => Object.keys(table[language])));
  for (const key of everyKey) {
    for (const language of languages) {
      if (!(key in table[language])) report(errors, `Web: "${key}" is missing in ${language}`, path, lineOfKey(key));
    }
  }

  const namespaces = new Set(Object.keys(table.en).map((key) => key.split(".")[0]));
  const used = new Map();
  for (const file of walk(web, [".html", ".js"])) {
    if (file === path) continue;
    const text = readFileSync(file, "utf8");
    const record = (key, index) => {
      if (!used.has(key)) used.set(key, { file, line: lineOf(text, index) });
    };

    for (const pattern of [/data-i18n(?:-aria|-name)?="([^"]+)"/g, /\bt\(\s*["'`]([^"'`$]+)["'`]/g]) {
      for (const match of text.matchAll(pattern)) record(match[1], match.index);
    }

    if (file.endsWith(".js")) {
      for (const match of text.matchAll(/["'`]([a-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)["'`]/g)) {
        const segments = match[1].split(".");
        if (!namespaces.has(segments[0])) continue;
        if (webFileExtensions.has(segments.at(-1).toLowerCase())) continue;
        record(match[1], match.index);
      }
    }
  }
  for (const match of source.matchAll(/\bt\(\s*["'`]([^"'`$]+)["'`]/g)) {
    if (!used.has(match[1])) used.set(match[1], { file: path, line: lineOf(source, match.index) });
  }

  for (const [key, where] of used) {
    if (!(key in table.en)) report(errors, `Web: "${key}" is used but not defined`, where.file, where.line);
  }
  for (const key of Object.keys(table.en)) {
    if (!used.has(key)) report(notes, `Web: "${key}" looks unused`, path, lineOfKey(key));
  }
}

checkApp();
checkWeb();

const print = (entry, level) => {
  const location = entry.file ? `${shown(entry.file)}:${entry.line ?? 1}` : "";
  if (annotate) {
    const kind = level === "error" ? "error" : "notice";
    console.log(`::${kind} file=${shown(entry.file)},line=${entry.line ?? 1}::${entry.message}`);
  } else {
    console.log(`${level.toUpperCase().padEnd(6)} ${location}  ${entry.message}`);
  }
};

notes.forEach((entry) => print(entry, "note"));
errors.forEach((entry) => print(entry, "error"));

const errorCount = errors.length;
const noteCount = notes.length;
const errorLabel = errorCount === 1 ? "error" : "errors";
const noteLabel = noteCount === 1 ? "note" : "notes";

console.log(`\n${errorCount} ${errorLabel}, ${noteCount} ${noteLabel}`);
process.exit(errors.length > 0 ? 1 : 0);
