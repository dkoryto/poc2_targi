import { useEffect, useRef, useState } from 'react';
import maplibregl, { Map as MlMap, Marker, Popup } from 'maplibre-gl';
import { useTranslation } from 'react-i18next';
import s from './dashboard.module.css';
import type { MapData, RiskCategory } from '@/api/types';
import { Info } from 'lucide-react';
import { riskColorVar, useIsMobile } from '@/components/ui';
import { fmtDate } from '@/lib/format';
import i18n from '@/i18n';
import { readThemeColor, THEME_EVENT } from '@/theme/theme';

const RISK_TOKEN: Record<RiskCategory, string> = {
  Low: '--risk-low',
  Medium: '--risk-medium',
  High: '--risk-high',
  Critical: '--risk-critical',
};

/**
 * MapLibre paints with WebGL and cannot resolve `var()`, so the palette is read from
 * the CSS custom properties at render time and refreshed on `dspc:themechange`.
 */
function riskHex(c: RiskCategory): string {
  return readThemeColor(RISK_TOKEN[c], '#888888');
}

function pointAlong(route: [number, number][], progress: number): [number, number] {
  if (route.length === 0) return [0, 0];
  if (route.length === 1) return route[0]!;
  const segs: number[] = [];
  let total = 0;
  for (let i = 1; i < route.length; i++) {
    const [x1, y1] = route[i - 1]!;
    const [x2, y2] = route[i]!;
    const d = Math.hypot(x2 - x1, y2 - y1);
    segs.push(d);
    total += d;
  }
  let target = Math.max(0, Math.min(1, progress)) * total;
  for (let i = 0; i < segs.length; i++) {
    const d = segs[i]!;
    if (target <= d || i === segs.length - 1) {
      const f = d === 0 ? 0 : target / d;
      const [x1, y1] = route[i]!;
      const [x2, y2] = route[i + 1]!;
      return [x1 + (x2 - x1) * f, y1 + (y2 - y1) * f];
    }
    target -= d;
  }
  return route[route.length - 1]!;
}

export interface OtherSite {
  code: string;
  name: string;
  city: string;
  lat: number;
  lon: number;
}

export function DeliveryMap({
  data,
  pulseCodes,
  onOpenPo,
  otherSites = [],
  onSelectSite,
}: {
  data: MapData;
  pulseCodes: Set<string>;
  onOpenPo: (poCode: string) => void;
  /** The organisation's other plants, drawn subdued; clicking one switches to it. */
  otherSites?: OtherSite[];
  onSelectSite?: (code: string) => void;
}) {
  const { t } = useTranslation();
  const isMobile = useIsMobile();
  const [legendOpen, setLegendOpen] = useState(false);
  const el = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MlMap | null>(null);
  const markersRef = useRef<Marker[]>([]);
  // Labels are decluttered after every camera change; lower rank wins a collision.
  const labelsRef = useRef<{ el: HTMLElement; rank: number }[]>([]);
  const readyRef = useRef(false);

  useEffect(() => {
    if (!el.current || mapRef.current) return;
    const map = new maplibregl.Map({
      container: el.current,
      style: {
        version: 8,
        sources: { europe: { type: 'geojson', data: '/geo/europe.geojson' } },
        layers: [
          { id: 'bg', type: 'background', paint: { 'background-color': readThemeColor('--map-bg', '#0b1018') } },
          { id: 'land', type: 'fill', source: 'europe', paint: { 'fill-color': readThemeColor('--map-land', '#18212e'), 'fill-opacity': 1 } },
          { id: 'borders', type: 'line', source: 'europe', paint: { 'line-color': readThemeColor('--map-border', '#2f3c4f'), 'line-width': 1 } },
        ],
      },
      center: [data.site.lon, data.site.lat],
      zoom: 3.4,
      attributionControl: false,
      dragRotate: false,
    });
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    map.on('moveend', declutterLabels);
    map.on('zoomend', declutterLabels);
    map.on('load', () => {
      // StrictMode mounts twice: a discarded map can still fire `load` after the live one
      // and would then steal the markers onto an already-removed instance.
      if (mapRef.current !== map) return;
      readyRef.current = true;
      map.addSource('routes', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
      map.addLayer({ id: 'routes', type: 'line', source: 'routes', paint: { 'line-color': ['get', 'color'], 'line-width': 2, 'line-dasharray': [2, 2], 'line-opacity': 0.9 } });
      map.addSource('done', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
      map.addLayer({ id: 'done', type: 'line', source: 'done', paint: { 'line-color': ['get', 'color'], 'line-width': 3, 'line-opacity': 0.9 } });
      render(map);
      requestAnimationFrame(declutterLabels);
    });
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null;
      readyRef.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /**
   * Hide labels that would overlap one already placed. Without this, plants and suppliers that
   * sit close together (Kielce/Zamość/Leszno, Gliwice/Kraków) print their names on top of each
   * other and none of them can be read.
   */
  const declutterLabels = () => {
    const placed: DOMRect[] = [];
    const overlaps = (a: DOMRect, b: DOMRect) =>
      a.left < b.right + 2 && a.right + 2 > b.left && a.top < b.bottom + 2 && a.bottom + 2 > b.top;
    for (const { el } of [...labelsRef.current].sort((a, b) => a.rank - b.rank)) {
      el.style.visibility = 'visible';
      const box = el.getBoundingClientRect();
      if (box.width === 0) continue;
      if (placed.some((p) => overlaps(box, p))) el.style.visibility = 'hidden';
      else placed.push(box);
    }
  };

  const render = (map: MlMap) => {
    markersRef.current.forEach((m) => m.remove());
    markersRef.current = [];
    labelsRef.current = [];
    const routeFeatures = data.shipments.map((sh) => ({ type: 'Feature' as const, properties: { color: riskHex(sh.riskCategory) }, geometry: { type: 'LineString' as const, coordinates: sh.route.length >= 2 ? sh.route : [sh.route[0] ?? [sh.lon, sh.lat], [data.site.lon, data.site.lat]] } }));
    const doneFeatures = data.shipments.map((sh) => {
      const route = sh.route.length >= 2 ? sh.route : [[sh.lon, sh.lat] as [number, number], [data.site.lon, data.site.lat] as [number, number]];
      const p = pointAlong(route, sh.progress);
      return { type: 'Feature' as const, properties: { color: riskHex(sh.riskCategory) }, geometry: { type: 'LineString' as const, coordinates: [route[0]!, p] } };
    });
    (map.getSource('routes') as maplibregl.GeoJSONSource | undefined)?.setData({ type: 'FeatureCollection', features: routeFeatures });
    (map.getSource('done') as maplibregl.GeoJSONSource | undefined)?.setData({ type: 'FeatureCollection', features: doneFeatures });

    // The active plant is the one the whole screen is about, so it gets a label of its own and
    // the highest priority when labels are decluttered. Without it the nearest other plant's
    // label sat next to this marker and read as if it named this one.
    const siteWrap = document.createElement('div');
    siteWrap.className = s.markerWrapSite ?? '';
    const site = document.createElement('div');
    site.className = s.markerSite ?? "";
    site.setAttribute('aria-label', data.site.name);
    site.title = `${data.site.code} · ${data.site.name}`;
    const siteLabel = document.createElement('span');
    siteLabel.className = s.markerLabel ?? "";
    siteLabel.textContent = data.site.name;
    labelsRef.current.push({ el: siteLabel, rank: 0 });
    siteWrap.append(site, siteLabel);
    markersRef.current.push(new Marker({ element: siteWrap, anchor: 'center' }).setLngLat([data.site.lon, data.site.lat]).addTo(map));

    for (const other of otherSites) {
      if (other.code === data.site.code) continue;
      const wrap = document.createElement('div');
      wrap.className = s.markerWrapSite ?? '';
      const dot = document.createElement('button');
      dot.type = 'button';
      dot.className = s.markerSiteOther ?? '';
      dot.setAttribute('aria-label', `${other.code} ${other.name}`);
      dot.dataset.testid = `map-site-${other.code}`;
      dot.title = `${other.code} · ${other.name}`;
      dot.onclick = () => onSelectSite?.(other.code);
      const label = document.createElement('span');
      label.className = [s.markerLabel, s.markerLabelMuted].filter(Boolean).join(' ');
      labelsRef.current.push({ el: label, rank: 1 });
      label.textContent = other.name;
      wrap.append(dot, label);
      markersRef.current.push(new Marker({ element: wrap, anchor: 'center' }).setLngLat([other.lon, other.lat]).addTo(map));
    }

    for (const sup of data.suppliers) {
      // The marker element must stay exactly dot-sized: MapLibre centres it on the coordinate,
      // so any element that stretches (a bare block div) drags the dot off its true position.
      const wrap = document.createElement('div');
      wrap.className = s.markerWrap ?? '';
      const dot = document.createElement('button');
      dot.type = 'button';
      dot.className = s.marker ?? "";
      dot.style.background = riskHex(sup.riskCategory);
      dot.setAttribute('aria-label', `${sup.code} ${sup.name} ${i18n.t(`risk.${sup.riskCategory}`)}`);
      dot.dataset.testid = `map-supplier-${sup.code}`;
      if (pulseCodes.has(sup.code)) dot.classList.add('pulse');
      const label = document.createElement('span');
      label.className = s.markerLabel ?? "";
      labelsRef.current.push({ el: label, rank: 2 });
      label.textContent = `${sup.code} · ${sup.city}`;
      wrap.append(dot, label);
      const popup = new Popup({ offset: 12, closeButton: true }).setHTML(
        `<div><strong>${sup.name}</strong><br/><span style="color:var(--fg-2)">${sup.code} · ${sup.city}, ${sup.country}</span><br/>${i18n.t('risk.score')}: <strong style="color:${riskHex(sup.riskCategory)}">${i18n.t(`risk.${sup.riskCategory}`)} · ${Math.round(sup.riskScore)}</strong></div>`,
      );
      markersRef.current.push(new Marker({ element: wrap, anchor: 'center' }).setLngLat([sup.lon, sup.lat]).setPopup(popup).addTo(map));
    }

    for (const sh of data.shipments) {
      const route = sh.route.length >= 2 ? sh.route : [[sh.lon, sh.lat] as [number, number], [data.site.lon, data.site.lat] as [number, number]];
      const [lon, lat] = pointAlong(route, sh.progress);
      const shipWrap = document.createElement('div');
      shipWrap.className = s.markerWrapShipment ?? '';
      const m = document.createElement('button');
      m.type = 'button';
      m.className = s.markerShipment ?? "";
      m.style.background = riskHex(sh.riskCategory);
      m.setAttribute('aria-label', `${sh.code} ${sh.partCode} ${i18n.t(`risk.${sh.riskCategory}`)}`);
      m.dataset.testid = `map-shipment-${sh.code}`;
      if (pulseCodes.has(sh.code) || pulseCodes.has(sh.poCode)) m.classList.add('pulse');
      const popupEl = document.createElement('div');
      popupEl.innerHTML = `<div><strong>${sh.code}</strong> · ${sh.poCode}<br/><span style="color:var(--fg-2)">${sh.partCode} × ${sh.quantity}</span><br/>${i18n.t('dashboard.popupEta')}: <strong>${fmtDate(sh.eta)}</strong> · ${i18n.t('dashboard.popupRequired')}: ${fmtDate(sh.requiredDate)}<br/>${i18n.t('risk.score')}: <strong style="color:${riskHex(sh.riskCategory)}">${i18n.t(`risk.${sh.riskCategory}`)} · ${Math.round(sh.riskScore)}</strong> <span style="color:var(--fg-3)">(${i18n.t('app.ruleBased')})</span></div>`;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.textContent = i18n.t('dashboard.openPo');
      btn.style.cssText = 'margin-top:6px;background:var(--info);color:var(--on-accent);border:0;border-radius:4px;padding:4px 8px;font-size:12px;font-weight:600;cursor:pointer';
      btn.onclick = () => onOpenPo(sh.poCode);
      popupEl.appendChild(btn);
      shipWrap.appendChild(m);
      const popup = new Popup({ offset: 10 }).setDOMContent(popupEl);
      markersRef.current.push(new Marker({ element: shipWrap, anchor: 'center' }).setLngLat([lon, lat]).setPopup(popup).addTo(map));
    }
  };

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !readyRef.current) return;
    render(map);
    requestAnimationFrame(declutterLabels);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, pulseCodes, otherSites]);

  // Switching plant pans the map to it rather than leaving the camera on the previous one.
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const reduce = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    map.easeTo?.({ center: [data.site.lon, data.site.lat], duration: reduce ? 0 : 500 });
  }, [data.site.code, data.site.lat, data.site.lon]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    // resize alone can leave the canvas blank when the panel was laid out after map init (focus mode, first paint)
    const ro = new ResizeObserver(() => { map.resize(); map.triggerRepaint(); });
    if (el.current) ro.observe(el.current);
    const onWindowResize = () => { map.resize(); map.triggerRepaint(); };
    window.addEventListener('resize', onWindowResize);
    return () => {
      ro.disconnect();
      window.removeEventListener('resize', onWindowResize);
    };
  }, []);

  // WebGL keeps its own copy of the palette: repaint the style layers and re-render the
  // markers/routes with the new theme colours instead of recreating the map.
  useEffect(() => {
    const onThemeChange = () => {
      const map = mapRef.current;
      if (!map || !readyRef.current) return;
      map.setPaintProperty('bg', 'background-color', readThemeColor('--map-bg', '#0b1018'));
      map.setPaintProperty('land', 'fill-color', readThemeColor('--map-land', '#18212e'));
      map.setPaintProperty('borders', 'line-color', readThemeColor('--map-border', '#2f3c4f'));
      render(map);
      requestAnimationFrame(declutterLabels);
      map.triggerRepaint();
    };
    document.addEventListener(THEME_EVENT, onThemeChange);
    return () => document.removeEventListener(THEME_EVENT, onThemeChange);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, pulseCodes, otherSites]);

  return (
    <>
      <div ref={el} className={s.map} data-testid="delivery-map" />
      {/* On a phone the legend would cover the map, so it folds behind this button. */}
      <button
        type="button"
        className={s.legendToggle}
        aria-expanded={legendOpen}
        onClick={() => setLegendOpen((o) => !o)}
        data-testid="map-legend-toggle"
      >
        <Info size={13} aria-hidden />
        {legendOpen ? t('common.hideLegend') : t('common.showLegend')}
      </button>
      <div className={s.mapLegend} aria-label={t('dashboard.legend')} hidden={isMobile && !legendOpen}>
        <div className={s.legendRow}><span className={s.legendSquare} />{t('dashboard.legendSite')}</div>
        {otherSites.length > 0 && <div className={s.legendRow}><span className={s.legendSquareMuted} />{t('dashboard.legendOtherSites')}</div>}
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Low') }} />{t('dashboard.legendSupplier')} · {t('risk.Low')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Medium') }} />{t('risk.Medium')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('High') }} />{t('risk.High')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Critical') }} />{t('risk.Critical')}</div>
        <div className={s.legendRow}><span className={s.legendLine} />{t('dashboard.legendRoute')} / {t('dashboard.legendShipment')}</div>
      </div>
    </>
  );
}
