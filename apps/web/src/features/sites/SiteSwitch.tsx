import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Building2, Check, ChevronDown } from 'lucide-react';
import s from './sites.module.css';
import { useSite, useSiteLabel } from './sites';

/** Localized plant profile ("montaż i integracja"), falling back to nothing when absent. */
export function useProfileLabel(): (profileKey: string | null | undefined) => string {
  const { t } = useTranslation();
  return (profileKey) => (profileKey ? t(`sites.profile.${profileKey}`, { defaultValue: '' }) : '');
}

export function SiteSwitch() {
  const { t } = useTranslation();
  const { sites, activeSite, activeSiteCode, setActiveSite } = useSite();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const profileLabel = useProfileLabel();

  useEffect(() => {
    if (!open) return;
    const onDoc = (e: MouseEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDoc);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  // A single plant (or an API without /sites) needs no switcher.
  if (sites.length <= 1) {
    return activeSite ? (
      <span className={s.single} data-testid="site-switch" title={activeSite.name}>
        <Building2 size={14} aria-hidden />
        <span>{activeSite.name === activeSite.code ? activeSite.code : `${activeSite.code} · ${activeSite.name}`}</span>
      </span>
    ) : null;
  }

  return (
    <div className={s.wrap} ref={wrapRef}>
      <button
        type="button"
        className={s.button}
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={t('sites.switchLabel')}
        title={t('sites.switchLabel')}
        data-testid="site-switch"
      >
        <Building2 size={14} aria-hidden />
        <span className={s.buttonText}>
          <strong>{activeSite?.name ?? activeSiteCode}</strong>
          <span>{activeSite?.city ?? ''}</span>
        </span>
        <ChevronDown size={13} aria-hidden />
      </button>
      {open && (
        <div className={s.menu} role="listbox" aria-label={t('sites.switchLabel')}>
          <div className={s.menuTitle}>{t('sites.menuTitle')}</div>
          {sites.map((site) => {
            const active = site.code === activeSiteCode;
            const profile = profileLabel(site.profileKey);
            return (
              <button
                key={site.code}
                type="button"
                role="option"
                aria-selected={active}
                className={s.option}
                onClick={() => {
                  setActiveSite(site.code);
                  setOpen(false);
                }}
                data-testid={`site-option-${site.code}`}
              >
                <span className={s.optionCheck}>{active && <Check size={13} aria-hidden />}</span>
                <span className={s.optionText}>
                  <strong>{site.name}</strong>
                  <span>
                    {site.code} · {site.city}
                    {profile ? ` · ${profile}` : ''}
                  </span>
                </span>
                {typeof site.highRiskDeliveries === 'number' && site.highRiskDeliveries > 0 && (
                  <span className={s.optionHint} title={t('kpi.HIGH_RISK_DELIVERIES')}>
                    {site.highRiskDeliveries}
                  </span>
                )}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

/**
 * Names the plant a record belongs to and, when that is not the plant currently selected, offers to
 * switch to it. Records are reachable by deep link and by scanning a passport QR, so the reader can
 * easily arrive with another plant active; this is an orientation aid, not an error.
 */
export function RecordSite({ code, className }: { code: string | null | undefined; className?: string }) {
  const { activeSiteCode, setActiveSite } = useSite();
  const { t } = useTranslation();
  const siteName = useSiteLabel();
  if (!code) return null;
  const foreign = code !== activeSiteCode;
  return (
    <span className={[s.recordSite, className].filter(Boolean).join(' ')} data-testid="record-site">
      <SiteChip code={code} />
      {foreign && (
        <button type="button" className={s.recordSiteSwitch} onClick={() => setActiveSite(code)} data-testid="record-site-switch">
          {t('sites.switchToThis', { site: siteName(code) })}
        </button>
      )}
    </span>
  );
}

/** Small inline chip naming a plant — used wherever a record may belong to another plant. */
export function SiteChip({ code, className }: { code: string | null | undefined; className?: string }) {
  const { sites, activeSiteCode } = useSite();
  const { t } = useTranslation();
  if (!code) return null;
  const site = sites.find((x) => x.code === code);
  return (
    <span className={[s.chip, code !== activeSiteCode && s.chipForeign, className].filter(Boolean).join(' ')} title={t('sites.recordSite', { site: site?.name ?? code })}>
      <Building2 size={11} aria-hidden />
      {site?.name ?? code}
    </span>
  );
}
