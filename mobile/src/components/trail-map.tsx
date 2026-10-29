import Mapbox, { Camera, CircleLayer, LineLayer, LocationPuck, MapView, PointAnnotation, ShapeSource, SymbolLayer } from '@rnmapbox/maps';
import { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { useProfile } from '@/api/profile';
import { Accent } from '@/components/ui';
import { config } from '@/config';
import { Radius } from '@/constants/theme';
import { toCameraBounds } from '@/lib/geo';
import type { Bounds, MapStyle, PointOfInterestType, PoiKind } from '@/lib/types';

Mapbox.setAccessToken(config.mapboxToken);

const STYLE_URLS: Record<MapStyle, string> = {
  Outdoors: 'mapbox://styles/mapbox/outdoors-v12',
  Terrain: 'mapbox://styles/mapbox/outdoors-v12',
  Satellite: 'mapbox://styles/mapbox/satellite-streets-v12',
};

export function useMapStyleUrl() {
  return STYLE_URLS[useProfile().data?.preferences.mapStyle ?? 'Outdoors'];
}

const POI_COLORS: Record<PointOfInterestType | PoiKind, string> = {
  WaterSource: '#2563EB',
  Shelter: '#92400E',
  Campsite: '#15803D',
  MountainHut: '#B45309',
  Hotel: '#DB2777',
  Viewpoint: '#9333EA',
  Other: '#475569',
};

export const poiColor = (type: PointOfInterestType | PoiKind) => POI_COLORS[type];

/** A coloured line drawn under the main route, e.g. an alternative the user can pick. */
export type MapVariant = { id: string; coordinates: Coordinate[]; color: string };

type Coordinate = { latitude: number; longitude: number };

export type MapMarker = Coordinate & { id: string; label?: string; color?: string };

type TrailMapProps = {
  /** One line per segment (a trail is one segment; a recorded hike has one per pause/resume). */
  lines?: Coordinate[][];
  /** A planned route drawn dashed under `lines`, e.g. the trail being followed while recording. */
  guide?: Coordinate[];
  pois?: (Coordinate & { id: string; name: string; type: PointOfInterestType | PoiKind })[];
  variants?: MapVariant[];
  markers?: MapMarker[];
  /** Markers the user can drag (long-press, then move). Tapping one calls `onMarkerPress`. */
  draggableMarkers?: MapMarker[];
  onMarkerDragEnd?: (id: string, coordinate: Coordinate) => void;
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

export function TrailMap({ lines = [], guide, pois = [], variants = [], markers = [], draggableMarkers = [], bounds, center, zoom = 12, showUser, followUser, height = 280, onMarkerPress, onMarkerDragEnd, onMapPress }: TrailMapProps) {
  // An annotation stays "selected" after a tap and ignores the next one; remounting it after each tap resets that.
  const [tapCount, setTapCount] = useState(0);
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

  const variantShape = useMemo<GeoJSON.FeatureCollection>(
    () => ({
      type: 'FeatureCollection',
      features: variants
        .filter((v) => v.coordinates.length >= 2)
        .map((v) => ({ type: 'Feature', properties: { color: v.color }, geometry: { type: 'LineString', coordinates: v.coordinates.map((p) => [p.longitude, p.latitude]) } })),
    }),
    [variants],
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

  // Keyed on the values, not the object: a new bounds object on every render would snap the camera back while panning.
  const boundsKey = bounds ? [bounds.minLatitude, bounds.minLongitude, bounds.maxLatitude, bounds.maxLongitude].join() : '';
  const cameraBounds = useMemo(
    () => (bounds ? { ...toCameraBounds(bounds), paddingTop: 40, paddingBottom: 40, paddingLeft: 40, paddingRight: 40 } : undefined),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- boundsKey captures bounds
    [boundsKey],
  );

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
        {variants.length > 0 && (
          <ShapeSource key="variants" id="variants" shape={variantShape}>
            <LineLayer id="variant-line" style={{ lineColor: ['get', 'color'], lineWidth: 4, lineOpacity: 0.75, lineJoin: 'round', lineCap: 'round' }} />
          </ShapeSource>
        )}
        <ShapeSource key="lines" id="lines" shape={lineShape}>
          <LineLayer id="line-casing" style={{ lineColor: '#ffffff', lineWidth: 7, lineJoin: 'round', lineCap: 'round' }} />
          <LineLayer id="line" style={{ lineColor: Accent, lineWidth: 4, lineJoin: 'round', lineCap: 'round' }} />
        </ShapeSource>
        <ShapeSource
          key="points"
          id="points"
          shape={pointShape}
          onPress={onMarkerPress && pointShape.features.length > 0 ? (e) => onMarkerPress(String(e.features[0]?.properties?.id ?? '')) : undefined}>
          <CircleLayer id="point-circles" style={{ circleRadius: 7, circleColor: ['get', 'color'], circleStrokeColor: '#fff', circleStrokeWidth: 2 }} />
          <SymbolLayer
            id="point-labels"
            style={{ textField: ['get', 'label'], textSize: 12, textOffset: [0, 1.4], textHaloColor: '#fff', textHaloWidth: 1.5, textOptional: true }}
          />
        </ShapeSource>
        {draggableMarkers.map((m) => (
          <PointAnnotation
            // Annotations are drawn once as an image; a new key redraws them when their colour or label changes.
            key={`${m.id}-${m.color}-${m.label}-${tapCount}`}
            id={m.id}
            coordinate={[m.longitude, m.latitude]}
            draggable
            anchor={{ x: 0.5, y: PIN_DOT / 2 / PIN_HEIGHT }} // the dot, not the label below it, sits on the point
            onSelected={() => {
              setTapCount((n) => n + 1);
              onMarkerPress?.(m.id);
            }}
            onDragEnd={(e) => {
              const [longitude, latitude] = e.geometry.coordinates;
              onMarkerDragEnd?.(m.id, { latitude, longitude });
            }}>
            <View style={styles.pin}>
              <View style={[styles.pinDot, { backgroundColor: m.color ?? Accent }]} />
              <Text style={styles.pinLabel} numberOfLines={1}>
                {m.label ?? ''}
              </Text>
            </View>
          </PointAnnotation>
        ))}
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

const PIN_DOT = 18;
const PIN_HEIGHT = 36;

const styles = StyleSheet.create({
  pin: { alignItems: 'center', width: 96, height: PIN_HEIGHT },
  pinDot: { width: PIN_DOT, height: PIN_DOT, borderRadius: PIN_DOT / 2, borderWidth: 3, borderColor: '#fff', boxShadow: '0 1px 3px rgba(0,0,0,0.35)' },
  pinLabel: { fontSize: 12, fontWeight: '600', color: '#111', textShadowColor: '#fff', textShadowRadius: 3, height: PIN_HEIGHT - PIN_DOT, lineHeight: PIN_HEIGHT - PIN_DOT },
  container: { borderRadius: Radius.large, borderCurve: 'continuous', overflow: 'hidden' },
});
