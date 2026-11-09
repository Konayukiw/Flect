import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, extname, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../..", import.meta.url));
const annotate = process.env.GITHUB_ACTIONS === "true";
const errors = [];
const notes = [];

const report = (list, message, file, line) => list.push({ message, file, line });
const lineOf = (text, index) => text.slice(0, index).split("\n").length;
const shown = (file) => relative(root, file).split(sep).join("/");
const lowerFirst = (word) => word[0].toLowerCase() + word.slice(1);

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
const languages = ["en", "ja", "zh"];
const fixedPrefix = "fixed.";

function loadCatalogs() {
  const folder = join(root, "src", "I18n");
  return Object.fromEntries(
    languages.map((language) => {
      const path = join(folder, `${language}.json`);
      const text = readFileSync(path, "utf8");
      const sections = {};
      let current = null;
      text.split("\n").forEach((line, index) => {
        const section = line.match(/^ {2}"([^"]+)"\s*:/);
        const entry = line.match(/^ {4}"((?:[^"\\]|\\.)*)"\s*:/);
        if (section) {
          current = sections[section[1]] = new Map();
        } else if (entry && current) {
          if (current.has(entry[1])) report(errors, `"${entry[1]}" is defined twice in ${language}`, path, index + 1);
          current.set(entry[1], index + 1);
        }
      });
      JSON.parse(text);
      return [language, { path, sections }];
    }),
  );
}

const catalogs = loadCatalogs();

const tablesOf = (section) =>
  Object.fromEntries(
    languages.map((language) => [language, { path: catalogs[language].path, keys: catalogs[language].sections[section] ?? new Map() }]),
  );

const keysOf = (tables) => new Set(Object.values(tables).flatMap((table) => [...table.keys.keys()]));

function checkParity(label, tables, exempt = () => false) {
  for (const key of keysOf(tables)) {
    if (exempt(key)) continue;
    for (const [language, table] of Object.entries(tables)) {
      if (!table.keys.has(key)) {
        const owner = Object.values(tables).find((other) => other.keys.has(key));
        report(errors, `${label}: "${key}" is missing in ${language}`, owner.path, owner.keys.get(key));
      }
    }
  }
}

function reportUsage(label, table, used) {
  for (const [key, where] of used) {
    if (!table.keys.has(key)) report(errors, `${label}: "${key}" is used but not defined`, where.file, where.line);
  }
  for (const [key, line] of table.keys) {
    if (!used.has(key)) report(notes, `${label}: "${key}" looks unused`, table.path, line);
  }
}

function checkApp() {
  const folder = join(root, "src", "App", "I18n");
  const tables = tablesOf("app");
  const english = tables.en.keys;

  checkParity("App", tables, (key) => key.endsWith(".one"));
  for (const key of keysOf(tables)) {
    if (!key.endsWith(".one")) continue;
    const base = key.slice(0, -".one".length);
    if (!english.has(base)) {
      report(errors, `App: Plural form "${key}" has no base key "${base}"`, tables.en.path, english.get(key));
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
  const runtime = join(web, "js", "i18n.js");
  const generated = join(web, "js", "strings.js");
  const tables = tablesOf("web");
  const english = tables.en.keys;

  checkParity("Web", tables);

  const namespaces = new Set([...english.keys()].map((key) => key.split(".")[0]));
  const used = new Map();
  for (const file of walk(web, [".html", ".js"])) {
    if (file === runtime || file === generated) continue;
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

  const source = readFileSync(runtime, "utf8");
  for (const match of source.matchAll(/\bt\(\s*["'`]([^"'`$]+)["'`]/g)) {
    if (!used.has(match[1])) used.set(match[1], { file: runtime, line: lineOf(source, match.index) });
  }

  reportUsage("Web", tables.en, used);
}

function checkPreview() {
  const folder = join(root, "src", "Preview");
  const tables = tablesOf("preview");
  const used = new Map();
  const use = (key, file, line) => {
    if (!used.has(key)) used.set(key, { file, line });
  };

  checkParity("Preview", tables);

  for (const file of walk(join(folder, "web"), [".js"])) {
    const text = readFileSync(file, "utf8");
    for (const pattern of [/\bt\(\s*"([^"]+)"/g, /\bstrings\["([^"]+)"\]/g]) {
      for (const match of text.matchAll(pattern)) use(match[1], file, lineOf(text, match.index));
    }
    for (const match of text.matchAll(/`([a-z][A-Za-z0-9]*\.)\$\{/g)) {
      for (const key of tables.en.keys.keys()) {
        if (key.startsWith(match[1])) use(key, file, lineOf(text, match.index));
      }
    }
  }

  const session = join(folder, "Session.cpp");
  const native = readFileSync(session, "utf8");
  for (const match of native.matchAll(/PreviewStrings::k([A-Z][A-Za-z0-9]*?)(?:En|Ja|Zh)\b/g)) {
    use(`native.${lowerFirst(match[1])}`, session, lineOf(native, match.index));
  }

  reportUsage("Preview", tables.en, used);
}

function checkShell() {
  const file = join(root, "src", "Shell", "MenuLayout.cpp");
  const tables = tablesOf("shell");
  const text = readFileSync(file, "utf8");
  const used = new Map();
  const use = (key, index) => {
    if (!used.has(key)) used.set(key, { file, line: lineOf(text, index) });
  };

  checkParity("Shell", tables, (key) => key.startsWith(fixedPrefix));
  for (const language of languages.filter((language) => language !== "en")) {
    for (const [key, line] of tables[language].keys) {
      if (key.startsWith(fixedPrefix)) {
        report(errors, `Shell: "${key}" does not vary by language and belongs only in en`, tables[language].path, line);
      }
    }
  }

  for (const match of text.matchAll(/(?<![\w"])text\.([a-z][A-Za-z0-9]*)\b(?!\()/g)) use(match[1], match.index);
  for (const match of text.matchAll(/\bLabel::k([A-Z][A-Za-z0-9]*)/g)) use(`${fixedPrefix}${lowerFirst(match[1])}`, match.index);

  reportUsage("Shell", tables.en, used);
}

checkApp();
checkWeb();
checkPreview();
checkShell();

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
