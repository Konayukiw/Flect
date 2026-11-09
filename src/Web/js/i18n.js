let currentLang = "en";

const t = (key, vars) => {
  let v = I18N[currentLang] ? I18N[currentLang][key] : undefined;
  if (v === undefined) v = I18N.en[key];
  if (v === undefined) return key;
  if (vars) {
    for (const k of Object.keys(vars)) {
      v = String(v).split("{" + k + "}").join(vars[k]);
    }
  }
  return v;
};

const setLang = (lang, save = true) => {
  if (!I18N[lang]) lang = "en";
  currentLang = lang;
  document.documentElement.lang = lang;

  document.querySelectorAll("[data-i18n]").forEach((el) => {
    const v = t(el.dataset.i18n);
    if (el.tagName === "TITLE") el.textContent = v;
    else if (el.tagName === "META") el.setAttribute("content", v);
    else el.innerHTML = v;
  });

  document.querySelectorAll("[data-i18n-aria]").forEach((el) => {
    el.setAttribute("aria-label", t(el.dataset.i18nAria));
  });

  document.querySelectorAll("[data-i18n-name]").forEach((icon) => {
    const v = t(icon.dataset.i18nName);
    icon.dataset.name = v;
    const nm = icon.querySelector(".nm");
    if (nm) nm.textContent = v;
  });

  const sel = document.getElementById("langSelect");
  if (sel) sel.value = lang;

  if (save) { try { localStorage.setItem("flect-lang", lang); } catch {} }
  dispatchEvent(new CustomEvent("langchange"));
};

const detectLang = () => {
  const tags = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || ""];
  for (const tag of tags) {
    const base = String(tag).toLowerCase().split("-")[0];
    if (Object.hasOwn(I18N, base)) return base;
  }
  return "en";
};

(() => {
  let saved = null;
  try { saved = localStorage.getItem("flect-lang"); } catch {}
  setLang(saved && Object.hasOwn(I18N, saved) ? saved : detectLang(), false);
  const sel = document.getElementById("langSelect");
  if (sel) sel.addEventListener("change", () => setLang(sel.value));
})();
