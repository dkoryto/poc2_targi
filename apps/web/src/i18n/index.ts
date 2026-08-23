import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import pl from './pl.json';
import en from './en.json';

export type Locale = 'pl' | 'en';
const STORAGE_KEY = 'dspc.locale';

function stored(): Locale {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    return v === 'en' ? 'en' : 'pl';
  } catch {
    return 'pl';
  }
}

void i18n.use(initReactI18next).init({
  resources: { pl: { translation: pl }, en: { translation: en } },
  lng: stored(),
  fallbackLng: 'pl',
  interpolation: { escapeValue: false },
  returnNull: false,
});

export function setLocale(locale: Locale): void {
  try {
    localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    /* ignore */
  }
  void i18n.changeLanguage(locale);
  document.documentElement.lang = locale;
}

export function currentLocale(): Locale {
  return i18n.language === 'en' ? 'en' : 'pl';
}

export default i18n;
