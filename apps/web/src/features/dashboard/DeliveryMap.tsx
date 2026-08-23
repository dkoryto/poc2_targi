import { useEffect, useRef } from 'react';
import maplibregl, { Map as MlMap, Marker, Popup } from 'maplibre-gl';
import { useTranslation } from 'react-i18next';
import s from './dashboard.module.css';
import type { MapData, RiskCategory } from '@/api/types';
import { riskColorVar } from '@/components/ui';
import { fmtDate } from '@/lib/format';
import i18n from '@/i18n';

const RISK_HEX: Record<RiskCategory, string> = { Low: '#2dd4bf', Medium: '#f5b544', High: '#f0843c', Critical: '#f05252' };

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

export function DeliveryMap({ data, pulseCodes, onOpenPo }: { data: MapData; pulseCodes: Set<string>; onOpenPo: (poCode: string) => void }) {
  const { t } = useTranslation();
  const el = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MlMap | null>(null);
  const markersRef = useRef<Marker[]>([]);
  const readyRef = useRef(false);

  useEffect(() => {
    if (!el.current || mapRef.current) return;
    const map = new maplibregl.Map({
      container: el.current,
      style: {
        version: 8,
        sources: { europe: { type: 'geojson', data: '/geo/europe.geojson' } },
        layers: [
          { id: 'bg', type: 'background', paint: { 'background-color': '#0b1018' } },
          { id: 'land', type: 'fill', source: 'europe', paint: { 'fill-color': '#18212e', 'fill-opacity': 1 } },
          { id: 'borders', type: 'line', source: 'europe', paint: { 'line-color': '#2f3c4f', 'line-width': 1 } },
        ],
      },
      center: [14, 51],
      zoom: 3.4,
      attributionControl: false,
      dragRotate: false,
    });
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right');
    map.on('load', () => {
      readyRef.current = true;
      map.addSource('routes', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
      map.addLayer({ id: 'routes', type: 'line', source: 'routes', paint: { 'line-color': ['get', 'color'], 'line-width': 2, 'line-dasharray': [2, 2], 'line-opacity': 0.9 } });
      map.addSource('done', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } });
      map.addLayer({ id: 'done', type: 'line', source: 'done', paint: { 'line-color': ['get', 'color'], 'line-width': 3, 'line-opacity': 0.9 } });
      render(map);
    });
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null;
      readyRef.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const render = (map: MlMap) => {
    markersRef.current.forEach((m) => m.remove());
    markersRef.current = [];
    const routeFeatures = data.shipments.map((sh) => ({ type: 'Feature' as const, properties: { color: RISK_HEX[sh.riskCategory] }, geometry: { type: 'LineString' as const, coordinates: sh.route.length >= 2 ? sh.route : [sh.route[0] ?? [sh.lon, sh.lat], [data.site.lon, data.site.lat]] } }));
    const doneFeatures = data.shipments.map((sh) => {
      const route = sh.route.length >= 2 ? sh.route : [[sh.lon, sh.lat] as [number, number], [data.site.lon, data.site.lat] as [number, number]];
      const p = pointAlong(route, sh.progress);
      return { type: 'Feature' as const, properties: { color: RISK_HEX[sh.riskCategory] }, geometry: { type: 'LineString' as const, coordinates: [route[0]!, p] } };
    });
    (map.getSource('routes') as maplibregl.GeoJSONSource | undefined)?.setData({ type: 'FeatureCollection', features: routeFeatures });
    (map.getSource('done') as maplibregl.GeoJSONSource | undefined)?.setData({ type: 'FeatureCollection', features: doneFeatures });

    const site = document.createElement('div');
    site.className = s.markerSite ?? "";
    site.setAttribute('aria-label', data.site.name);
    site.title = `${data.site.code} · ${data.site.name}`;
    markersRef.current.push(new Marker({ element: site }).setLngLat([data.site.lon, data.site.lat]).addTo(map));

    for (const sup of data.suppliers) {
      const wrap = document.createElement('div');
      wrap.style.position = 'relative';
      const dot = document.createElement('button');
      dot.type = 'button';
      dot.className = s.marker ?? "";
      dot.style.background = RISK_HEX[sup.riskCategory];
      dot.setAttribute('aria-label', `${sup.code} ${sup.name} ${i18n.t(`risk.${sup.riskCategory}`)}`);
      dot.dataset.testid = `map-supplier-${sup.code}`;
      if (pulseCodes.has(sup.code)) dot.classList.add('pulse');
      const label = document.createElement('span');
      label.className = s.markerLabel ?? "";
      label.textContent = `${sup.code} · ${sup.city}`;
      wrap.append(dot, label);
      const popup = new Popup({ offset: 12, closeButton: true }).setHTML(
        `<div><strong>${sup.name}</strong><br/><span style="color:var(--fg-2)">${sup.code} · ${sup.city}, ${sup.country}</span><br/>${i18n.t('risk.score')}: <strong style="color:${RISK_HEX[sup.riskCategory]}">${i18n.t(`risk.${sup.riskCategory}`)} · ${Math.round(sup.riskScore)}</strong></div>`,
      );
      markersRef.current.push(new Marker({ element: wrap }).setLngLat([sup.lon, sup.lat]).setPopup(popup).addTo(map));
    }

    for (const sh of data.shipments) {
      const route = sh.route.length >= 2 ? sh.route : [[sh.lon, sh.lat] as [number, number], [data.site.lon, data.site.lat] as [number, number]];
      const [lon, lat] = pointAlong(route, sh.progress);
      const m = document.createElement('button');
      m.type = 'button';
      m.className = s.markerShipment ?? "";
      m.style.background = RISK_HEX[sh.riskCategory];
      m.setAttribute('aria-label', `${sh.code} ${sh.partCode} ${i18n.t(`risk.${sh.riskCategory}`)}`);
      m.dataset.testid = `map-shipment-${sh.code}`;
      if (pulseCodes.has(sh.code) || pulseCodes.has(sh.poCode)) m.classList.add('pulse');
      const popupEl = document.createElement('div');
      popupEl.innerHTML = `<div><strong>${sh.code}</strong> · ${sh.poCode}<br/><span style="color:var(--fg-2)">${sh.partCode} × ${sh.quantity}</span><br/>${i18n.t('dashboard.popupEta')}: <strong>${fmtDate(sh.eta)}</strong> · ${i18n.t('dashboard.popupRequired')}: ${fmtDate(sh.requiredDate)}<br/>${i18n.t('risk.score')}: <strong style="color:${RISK_HEX[sh.riskCategory]}">${i18n.t(`risk.${sh.riskCategory}`)} · ${Math.round(sh.riskScore)}</strong> <span style="color:var(--fg-3)">(${i18n.t('app.ruleBased')})</span></div>`;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.textContent = i18n.t('dashboard.openPo');
      btn.style.cssText = 'margin-top:6px;background:var(--info);color:#061021;border:0;border-radius:4px;padding:4px 8px;font-size:12px;font-weight:600;cursor:pointer';
      btn.onclick = () => onOpenPo(sh.poCode);
      popupEl.appendChild(btn);
      const popup = new Popup({ offset: 10 }).setDOMContent(popupEl);
      markersRef.current.push(new Marker({ element: m }).setLngLat([lon, lat]).setPopup(popup).addTo(map));
    }
  };

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !readyRef.current) return;
    render(map);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, pulseCodes]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const ro = new ResizeObserver(() => map.resize());
    if (el.current) ro.observe(el.current);
    return () => ro.disconnect();
  }, []);

  return (
    <>
      <div ref={el} className={s.map} data-testid="delivery-map" />
      <div className={s.mapLegend} aria-label={t('dashboard.legend')}>
        <div className={s.legendRow}><span className={s.legendSquare} />{t('dashboard.legendSite')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Low') }} />{t('dashboard.legendSupplier')} · {t('risk.Low')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Medium') }} />{t('risk.Medium')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('High') }} />{t('risk.High')}</div>
        <div className={s.legendRow}><span className={s.legendDot} style={{ background: riskColorVar('Critical') }} />{t('risk.Critical')}</div>
        <div className={s.legendRow}><span className={s.legendLine} />{t('dashboard.legendRoute')} / {t('dashboard.legendShipment')}</div>
      </div>
    </>
  );
}
