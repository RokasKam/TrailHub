import Mapbox, { Camera, CircleLayer, LineLayer, LocationPuck, MapView, ShapeSource, SymbolLayer } from '@rnmapbox/maps';
import { useMemo } from 'react';
import { StyleSheet, View } from 'react-native';

import { useProfile } from '@/api/profile';
import { Accent } from '@/components/ui';
import { config } from '@/config';
import { Spacing } from '@/constants/theme';
import { toCameraBounds } from '@/lib/geo';
import type { Bounds, MapStyle, PointOfInterestType } from '@/lib/types';

Mapbox.setAccessToken(config.mapboxToken);

const STYLE_URLS: Record<MapStyle, string> = {
  Outdoors: 'mapbox://styles/mapbox/outdoors-v12',
  Terrain: 'mapbox://styles/mapbox/outdoors-v12',
  Satellite: 'mapbox://styles/mapbox/satellite-streets-v12',
};

export function useMapStyleUrl() {
  return STYLE_URLS[useProfile().data?.preferences.mapStyle ?? 'Outdoors'];
}

const POI_COLORS: Record<PointOfInterestType, string> = {
  WaterSource: '#2563EB',
  Shelter: '#92400E',
  Campsite: '#15803D',
  Viewpoint: '#9333EA',
  Other: '#475569',
};

type Coordinate = { latitude: number; longitude: number };

export type MapMarker = Coordinate & { id: string; label?: string; color?: string };

type TrailMapProps = {
  /** One line per segment (a trail is one segment; a recorded hike has one per pause/resume). */
  lines?: Coordinate[][];
  /** A planned route drawn dashed under `lines`, e.g. the trail being followed while recording. */
  guide?: Coordinate[];
  pois?: (Coordinate & { id: string; name: string; type: PointOfInterestType })[];
  markers?: MapMarker[];
  bounds?: Bounds;
  center?: Coordinate;
  zoom?: number;
  showUser?: boolean;
  followUser?: boolean;
  height?: number;
  onMarkerPress?: (id: string) => void;
  /** Tap on empty map, e.g. to drop a meeting-point pin. */
  onMapPress?: (coordinate: Coordinate) => void;
};

export function TrailMap({ lines = [], guide, pois = [], markers = [], bounds, center, zoom = 12, showUser, followUser, height = 280, onMarkerPress, onMapPress }: TrailMapProps) {
  const styleURL = useMapStyleUrl();

  const lineShape = useMemo<GeoJSON.FeatureCollection>(
    () => ({
      type: 'FeatureCollection',
      features: lines
        .filter((l) => l.length >= 2)
        .map((l) => ({ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: l.map((p) => [p.longitude, p.latitude]) } })),
    }),
    [lines],
  );

  const guideShape = useMemo<GeoJSON.Feature<GeoJSON.LineString>>(
    () => ({ type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: (guide ?? []).map((p) => [p.longitude, p.latitude]) } }),
    [guide],
  );

  const pointShape = useMemo<GeoJSON.FeatureCollection>(
    () => ({
      type: 'FeatureCollection',
      features: [
        ...pois.map((p) => ({
          type: 'Feature' as const,
          id: p.id,
          properties: { id: p.id, label: p.name, color: POI_COLORS[p.type] },
          geometry: { type: 'Point' as const, coordinates: [p.longitude, p.latitude] },
        })),
        ...markers.map((m) => ({
          type: 'Feature' as const,
          id: m.id,
          properties: { id: m.id, label: m.label ?? '', color: m.color ?? Accent },
          geometry: { type: 'Point' as const, coordinates: [m.longitude, m.latitude] },
        })),
      ],
    }),
    [markers, pois],
  );

  const cameraBounds = bounds ? { ...toCameraBounds(bounds), paddingTop: 40, paddingBottom: 40, paddingLeft: 40, paddingRight: 40 } : undefined;

  return (
    <View style={[styles.container, { height }]}>
      <MapView
        style={StyleSheet.absoluteFill}
        styleURL={styleURL}
        scaleBarEnabled={false}
        compassEnabled
        onPress={(feature) => {
          if (!onMapPress || feature.geometry.type !== 'Point') return;
          const [longitude, latitude] = feature.geometry.coordinates;
          onMapPress({ latitude, longitude });
        }}>
        <Camera
          bounds={followUser ? undefined : cameraBounds}
          centerCoordinate={!cameraBounds && center ? [center.longitude, center.latitude] : undefined}
          zoomLevel={!cameraBounds ? zoom : undefined}
          followUserLocation={followUser}
          followZoomLevel={15}
          animationDuration={0}
        />
        {showUser && <LocationPuck puckBearingEnabled puckBearing="heading" pulsing={{ isEnabled: true }} />}
        {guide && guide.length >= 2 && (
          <ShapeSource key="guide" id="guide" shape={guideShape}>
            <LineLayer id="guide-line" style={{ lineColor: '#6B7280', lineWidth: 3, lineDasharray: [2, 1.5], lineJoin: 'round', lineOpacity: 0.8 }} />
          </ShapeSource>
        )}
        <ShapeSource key="lines" id="lines" shape={lineShape}>
          <LineLayer id="line-casing" style={{ lineColor: '#ffffff', lineWidth: 7, lineJoin: 'round', lineCap: 'round' }} />
          <LineLayer id="line" style={{ lineColor: Accent, lineWidth: 4, lineJoin: 'round', lineCap: 'round' }} />
        </ShapeSource>
        <ShapeSource key="points" id="points" shape={pointShape} onPress={(e) => onMarkerPress?.(String(e.features[0]?.properties?.id ?? ''))}>
          <CircleLayer id="point-circles" style={{ circleRadius: 7, circleColor: ['get', 'color'], circleStrokeColor: '#fff', circleStrokeWidth: 2 }} />
          <SymbolLayer
            id="point-labels"
            style={{ textField: ['get', 'label'], textSize: 12, textOffset: [0, 1.4], textHaloColor: '#fff', textHaloWidth: 1.5, textOptional: true }}
          />
        </ShapeSource>
      </MapView>
    </View>
  );
}

/**
 * Saves map tiles around a trail for offline use (zoom 10–15 covers overview to trail detail).
 * Route, details and elevation are cached separately by the query persister.
 */
/** Download progress (0–100) of a saved offline region, or null if it was never saved. */
export async function offlineRegionProgress(name: string): Promise<number | null> {
  const pack = await Mapbox.offlineManager.getPack(name).catch(() => undefined);
  if (!pack) return null;
  const status = await pack.status();
  return Math.round(status.percentage ?? 0);
}

export async function downloadOfflineRegion(name: string, bounds: Bounds, styleURL: string, onProgress: (percent: number) => void) {
  const pad = 0.02; // ~2 km margin around the route
  const existing = await Mapbox.offlineManager.getPack(name);
  if (existing) await Mapbox.offlineManager.deletePack(name);
  await Mapbox.offlineManager.createPack(
    {
      name,
      styleURL,
      minZoom: 10,
      maxZoom: 15,
      bounds: [
        [bounds.maxLongitude + pad, bounds.maxLatitude + pad],
        [bounds.minLongitude - pad, bounds.minLatitude - pad],
      ],
    },
    (_pack, status) => onProgress(Math.round(status.percentage ?? 0)),
    (_pack, error) => {
      throw new Error(error.message);
    },
  );
}

const styles = StyleSheet.create({
  container: { borderRadius: Spacing.three, overflow: 'hidden' },
});
