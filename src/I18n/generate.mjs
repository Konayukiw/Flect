import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const folder = dirname(fileURLToPath(import.meta.url));
const languages = ["en", "ja", "zh"];
const dictionaryNames = { en: "English", ja: "Japanese", zh: "ChineseSimplified" };
const fixedPrefix = "fixed.";
const nativePrefix = "native.";

const catalogs = Object.fromEntries(
  languages.map((language) => [language, JSON.parse(readFileSync(join(folder, `${language}.json`), "utf8"))]),
);

const quote = (text) => JSON.stringify(text);
const wide = (text) => `L${quote(text)}`;
const capitalize = (word) => word[0].toUpperCase() + word.slice(1);
const pretty = (value) => JSON.stringify(value, null, 2);
const tagged = (keep, name) =>
  Object.fromEntries(
    languages.map((language) => [
      language,
      Object.fromEntries(Object.entries(catalogs[language][name]).filter(([key]) => keep(key))),
    ]),
  );

function required(name, language, key) {
  const value = catalogs[language][name][key];
  if (value === undefined) throw new Error(`${language}.json: "${key}" is missing from "${name}"`);
  return value;
}

function app() {
  const lines = ["namespace Flect.I18n;", "", "internal static class Strings", "{"];
  languages.forEach((language, index) => {
    if (index > 0) lines.push("");
    lines.push(`    public static readonly IReadOnlyDictionary<string, string> ${dictionaryNames[language]} = new Dictionary<string, string>`, "    {");
    for (const [key, value] of Object.entries(catalogs[language].app)) {
      lines.push(`        [${quote(key)}] = ${quote(value)},`);
    }
    lines.push("    };");
  });
  lines.push("}", "");
  return lines.join("\n");
}

function web() {
  return `const I18N = ${pretty(tagged(() => true, "web"))};\n`;
}

function preview() {
  return `export const STRINGS = ${pretty(tagged((key) => !key.startsWith(nativePrefix), "preview"))};\n`;
}

function native() {
  const keys = Object.keys(catalogs.en.preview).filter((key) => key.startsWith(nativePrefix));
  const lines = ["#pragma once", "", "namespace PreviewStrings {", ""];
  for (const key of keys) {
    const name = capitalize(key.slice(nativePrefix.length));
    for (const language of languages) {
      lines.push(`inline constexpr const wchar_t* k${name}${capitalize(language)} = ${wide(required("preview", language, key))};`);
    }
  }
  lines.push("", "}", "");
  return lines.join("\n");
}

function shell() {
  const english = Object.keys(catalogs.en.shell);
  const fixed = english.filter((key) => key.startsWith(fixedPrefix));
  const fields = english.filter((key) => !key.startsWith(fixedPrefix));
  const lines = ["#pragma once", "", "namespace Label {", ""];
  for (const key of fixed) {
    lines.push(`inline constexpr const wchar_t* k${capitalize(key.slice(fixedPrefix.length))} = ${wide(catalogs.en.shell[key])};`);
  }
  lines.push("", "}", "", "struct LabelTable {");
  for (const field of fields) lines.push(`  const wchar_t* ${field};`);
  lines.push("};", "", "namespace Labels {");
  for (const language of languages) {
    lines.push("", `inline constexpr LabelTable k${capitalize(language)} = {`);
    for (const field of fields) lines.push(`    ${wide(required("shell", language, field))},`);
    lines.push("};");
  }
  lines.push("", "}", "");
  return lines.join("\n");
}

const targets = { app, web, preview, native, shell };
const [target, output] = process.argv.slice(2);

if (!Object.hasOwn(targets, target) || !output) {
  console.error(`Usage: node generate.mjs <${Object.keys(targets).join("|")}> <output>`);
  process.exit(2);
}

const text = targets[target]();
mkdirSync(dirname(output), { recursive: true });
if (!existsSync(output) || readFileSync(output, "utf8") !== text) writeFileSync(output, text);
