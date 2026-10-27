import type { Units } from '@/lib/types';

export function formatDistance(meters: number, units: Units = 'Metric') {
  if (units === 'Imperial') {
    const miles = meters / 1609.344;
    return miles < 0.1 ? `${Math.round(meters * 3.28084)} ft` : `${miles.toFixed(1)} mi`;
  }
  return meters < 1000 ? `${Math.round(meters)} m` : `${(meters / 1000).toFixed(1)} km`;
}

export function formatElevation(meters: number, units: Units = 'Metric') {
  return units === 'Imperial' ? `${Math.round(meters * 3.28084).toLocaleString()} ft` : `${Math.round(meters).toLocaleString()} m`;
}

export function formatDuration(totalSeconds: number) {
  const s = Math.max(0, Math.round(totalSeconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return h > 0 ? `${h}h ${m.toString().padStart(2, '0')}m` : `${m}m ${(s % 60).toString().padStart(2, '0')}s`;
}

export function formatMinutes(minutes: number) {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export function formatPace(metersPerSecond: number, units: Units = 'Metric') {
  if (metersPerSecond <= 0) return '–';
  return units === 'Imperial' ? `${(metersPerSecond * 2.23694).toFixed(1)} mph` : `${(metersPerSecond * 3.6).toFixed(1)} km/h`;
}

export function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}
