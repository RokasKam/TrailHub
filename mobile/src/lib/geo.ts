import type { TrackPoint } from '@/lib/types';

const EARTH_RADIUS_M = 6_371_008.8;
const toRad = (deg: number) => (deg * Math.PI) / 180;

/** Same haversine + 3 m elevation-noise threshold as the backend, so live stats match the saved ones. */
export function haversineMeters(a: { latitude: number; longitude: number }, b: { latitude: number; longitude: number }) {
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const h = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) * Math.sin(dLon / 2) ** 2;
  return 2 * EARTH_RADIUS_M * Math.asin(Math.min(1, Math.sqrt(h)));
}

const ELEVATION_NOISE_M = 3;

export function trackMetrics(segments: TrackPoint[][], now = Date.now()) {
  let distance = 0;
  let gain = 0;
  let movingMs = 0;
  segments.forEach((points, index) => {
    let anchor: number | null = null;
    for (let i = 0; i < points.length; i++) {
      if (i > 0) distance += haversineMeters(points[i - 1], points[i]);
      const ele = points[i].elevation;
      if (ele == null) continue;
      if (anchor == null) anchor = ele;
      else if (ele - anchor >= ELEVATION_NOISE_M) {
        gain += ele - anchor;
        anchor = ele;
      } else if (anchor - ele >= ELEVATION_NOISE_M) anchor = ele;
    }
    if (points.length > 0) {
      // The open (last) segment keeps counting until now; closed ones end at their last fix.
      const isOpen = index === segments.length - 1;
      const end = isOpen ? now : Date.parse(points[points.length - 1].timestampUtc);
      movingMs += end - Date.parse(points[0].timestampUtc);
    }
  });
  return { distanceMeters: distance, elevationGainMeters: gain, movingSeconds: Math.max(0, movingMs / 1000) };
}

/** Mapbox [sw, ne] corners from a bounds DTO. */
export function toCameraBounds(b: { minLatitude: number; minLongitude: number; maxLatitude: number; maxLongitude: number }) {
  return { sw: [b.minLongitude, b.minLatitude] as [number, number], ne: [b.maxLongitude, b.maxLatitude] as [number, number] };
}

/** Bounding box of a list of coordinates (undefined when empty). */
export function boundsOf(points: { latitude: number; longitude: number }[]) {
  if (points.length === 0) return undefined;
  const lats = points.map((p) => p.latitude);
  const lons = points.map((p) => p.longitude);
  return { minLatitude: Math.min(...lats), minLongitude: Math.min(...lons), maxLatitude: Math.max(...lats), maxLongitude: Math.max(...lons) };
}
