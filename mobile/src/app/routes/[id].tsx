import * as Location from 'expo-location';
import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActionSheetIOS, Alert, Platform, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { approxMeters, downloadRouteGpx, useAlternatives, useDeleteRoute, usePoisAlong, useRoute, useRoutePreview, useSaveRoute } from '@/api/routes';
import { Itinerary, POI_LABELS, RouteElevation, RouteStats, RouteWarnings } from '@/components/route-summary';
import { ThemedText } from '@/components/themed-text';
import { TrailMap, type MapMarker } from '@/components/trail-map';
import { Button, Card, Chip, IconButton, Input, List, ListRow, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { describeError } from '@/lib/api';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import { shareTextFile } from '@/lib/share-file';
import type { Bounds, PlannedRoute, Poi, PoiKind, RouteWaypoint } from '@/lib/types';

/** Where to centre an empty map when we don't know where the user is: Chamonix. */
const FALLBACK_CENTER = { latitude: 45.9237, longitude: 6.8694 };
const ALTERNATIVE_COLORS = ['#2563EB', '#D97706', '#DB2777'];
const POI_FILTERS: { label: string; kinds: PoiKind[] }[] = [
  { label: 'Water', kinds: ['WaterSource'] },
  { label: 'Huts', kinds: ['MountainHut', 'Shelter'] },
  { label: 'Campsites', kinds: ['Campsite'] },
  { label: 'Hotels', kinds: ['Hotel'] },
  { label: 'Viewpoints', kinds: ['Viewpoint'] },
];
const SLEEPING: PoiKind[] = ['Campsite', 'MountainHut', 'Shelter', 'Hotel'];

function chooseAction(title: string, actions: { label: string; destructive?: boolean; run: () => void }[]) {
  if (Platform.OS === 'ios') {
    ActionSheetIOS.showActionSheetWithOptions(
      { title, options: [...actions.map((a) => a.label), 'Cancel'], cancelButtonIndex: actions.length, destructiveButtonIndex: actions.flatMap((a, i) => (a.destructive ? [i] : [])) },
      (i) => actions[i]?.run(),
    );
  } else {
    Alert.alert(title, undefined, [...actions.map((a) => ({ text: a.label, style: a.destructive ? ('destructive' as const) : undefined, onPress: a.run })), { text: 'Cancel', style: 'cancel' }]);
  }
}

function boundsOf(path: [number, number, number | null][]): Bounds | undefined {
  if (path.length < 2) return undefined;
  const lats = path.map((p) => p[0]);
  const lons = path.map((p) => p[1]);
  return { minLatitude: Math.min(...lats), minLongitude: Math.min(...lons), maxLatitude: Math.max(...lats), maxLongitude: Math.max(...lons) };
}

/**
 * Where a new point goes: if it's on (or near) the current line, between the waypoints it falls between, so tapping
 * the middle of a route bends it there; otherwise at the end, extending the route.
 */
function insertionIndex(route: PlannedRoute | undefined, waypoints: RouteWaypoint[], point: { latitude: number; longitude: number }) {
  if (!route || waypoints.length < 2) return waypoints.length;
  const tap: [number, number] = [point.latitude, point.longitude];
  let nearest = 0;
  let nearestDistance = Infinity;
  route.path.forEach((p, i) => {
    const d = approxMeters(p, tap);
    if (d < nearestDistance) [nearest, nearestDistance] = [i, d];
  });
  if (nearestDistance > 400) return waypoints.length;
  const indexOnPath = (w: RouteWaypoint) => {
    let best = 0;
    let bestD = Infinity;
    route.path.forEach((p, i) => {
      const d = approxMeters(p, [w.latitude, w.longitude]);
      if (d < bestD) [best, bestD] = [i, d];
    });
    return best;
  };
  const after = waypoints.findIndex((w, i) => i > 0 && indexOnPath(w) > nearest);
  return after === -1 ? waypoints.length : after;
}

/** Picks a few points along an alternative so routing through them reproduces it. */
function viaPointsOf(route: PlannedRoute): RouteWaypoint[] {
  const total = route.stats.distanceMeters;
  const out: RouteWaypoint[] = [];
  let run = 0;
  let next = 1;
  for (let i = 1; i < route.path.length && next <= 3; i++) {
    run += approxMeters(route.path[i - 1], route.path[i]);
    if (run >= (total * next) / 4) {
      out.push({ latitude: route.path[i][0], longitude: route.path[i][1], isOvernight: false });
      next++;
    }
  }
  return out;
}

export default function RouteEditorScreen() {
  const { id: param } = useLocalSearchParams<{ id: string }>();
  const id = param === 'new' ? null : param;
  const units = useUnits();
  const saved = useRoute(id);
  const save = useSaveRoute(id);
  const remove = useDeleteRoute();
  const findAlternatives = useAlternatives();

  // Edits live here until saved; until the first edit, the saved route is shown.
  const [edited, setEdited] = useState<RouteWaypoint[] | null>(null);
  const [name, setName] = useState<string | null>(null);
  const [poiFilters, setPoiFilters] = useState<string[]>(['Water', 'Huts', 'Campsites']);
  const [initialCenter, setInitialCenter] = useState<{ latitude: number; longitude: number } | null>(id ? null : FALLBACK_CENTER);
  // Fit the camera to a saved route once, when it first loads (not on every edit, which would fight the user).
  const [fitted, setFitted] = useState<{ id: string; bounds?: Bounds } | null>(null);
  if (saved.data?.id && fitted?.id !== saved.data.id) setFitted({ id: saved.data.id, bounds: boundsOf(saved.data.path) });

  const waypoints = useMemo(() => edited ?? saved.data?.waypoints ?? [], [edited, saved.data]);
  const preview = useRoutePreview(waypoints, edited !== null);
  const route = edited !== null ? (waypoints.length >= 2 ? preview.data : undefined) : saved.data;
  const kinds = POI_FILTERS.filter((f) => poiFilters.includes(f.label)).flatMap((f) => f.kinds);
  const pois = usePoisAlong(preview.isFetching ? undefined : route?.path, kinds);

  // Centre a new route on the user, if we know where they are.
  useEffect(() => {
    if (id) return;
    void Location.getLastKnownPositionAsync()
      .then((p) => p && setInitialCenter({ latitude: p.coords.latitude, longitude: p.coords.longitude }))
      .catch(() => undefined);
  }, [id]);

  if (id && !saved.data) return <Screen><QueryState isLoading={saved.isLoading} error={saved.error} onRetry={() => saved.refetch()} /></Screen>;

  const update = (next: RouteWaypoint[]) => {
    findAlternatives.reset();
    setEdited(next);
  };
  const insert = (w: RouteWaypoint) => {
    const at = insertionIndex(route, waypoints, w);
    update([...waypoints.slice(0, at), w, ...waypoints.slice(at)]);
  };

  const waypointActions = (i: number) => {
    const w = waypoints[i];
    const middle = i > 0 && i < waypoints.length - 1;
    chooseAction(w.name ?? (i === 0 ? 'Start' : i === waypoints.length - 1 ? 'Finish' : `Waypoint ${i + 1}`), [
      ...(middle ? [{ label: w.isOvernight ? 'Not an overnight stop' : 'Stay here overnight (end of a day)', run: () => update(waypoints.map((x, j) => (j === i ? { ...x, isOvernight: !x.isOvernight } : x))) }] : []),
      ...(i > 0 ? [{ label: 'Move earlier', run: () => update(waypoints.map((x, j) => (j === i - 1 ? waypoints[i] : j === i ? waypoints[i - 1] : x))) }] : []),
      ...(i < waypoints.length - 1 ? [{ label: 'Move later', run: () => update(waypoints.map((x, j) => (j === i + 1 ? waypoints[i] : j === i ? waypoints[i + 1] : x))) }] : []),
      { label: 'Remove', destructive: true, run: () => update(waypoints.filter((_, j) => j !== i)) },
    ]);
  };

  const poiActions = (p: Poi) =>
    chooseAction(`${p.name} · ${POI_LABELS[p.kind]}`, [
      { label: 'Go via here', run: () => insert({ latitude: p.latitude, longitude: p.longitude, name: p.name, isOvernight: false, kind: p.kind }) },
      ...(SLEEPING.includes(p.kind) ? [{ label: 'Stay here overnight', run: () => insert({ latitude: p.latitude, longitude: p.longitude, name: p.name, isOvernight: true, kind: p.kind }) }] : []),
    ]);

  const routeName = name ?? saved.data?.name ?? '';
  const doSave = (then?: (id: string) => void) => {
    const finalName = routeName.trim() || `Route ${new Date().toLocaleDateString()}`;
    save.mutate(
      { name: finalName, description: saved.data?.description ?? null, waypoints },
      {
        onSuccess: (r) => {
          setEdited(null);
          setName(null);
          if (!id) router.replace(`/routes/${r.id}`);
          then?.(r.id!);
        },
        onError: (e) => Alert.alert("Couldn't save", describeError(e)),
      },
    );
  };
  const exportGpx = () => {
    const run = async (routeId: string) => {
      try {
        await shareTextFile(`${(routeName || 'route').replace(/[^\w-]+/g, '-').toLowerCase()}.gpx`, await downloadRouteGpx(routeId));
      } catch (e) {
        Alert.alert('Export failed', describeError(e as Error));
      }
    };
    if (id && edited === null) void run(id);
    else doSave((savedId) => void run(savedId));
  };

  const markers: MapMarker[] = waypoints.map((w, i) => ({
    id: `wp-${i}`,
    latitude: w.latitude,
    longitude: w.longitude,
    label: i === 0 ? 'Start' : i === waypoints.length - 1 ? 'Finish' : w.isOvernight ? `Night ${waypoints.slice(0, i + 1).filter((x) => x.isOvernight).length}` : String(i + 1),
    color: i === 0 ? '#16A34A' : i === waypoints.length - 1 ? '#DC2626' : w.isOvernight ? '#7C3AED' : '#0F172A',
  }));
  const poiList = pois.data ?? [];
  const alternatives = findAlternatives.data ?? [];

  return (
    <Screen>
      <Stack.Screen
        options={{
          title: id ? 'Route' : 'New route',
          headerRight: () => <IconButton icon="arrow.uturn.backward" label="Undo last point" onPress={() => waypoints.length && update(waypoints.slice(0, -1))} />,
        }}
      />

      <TrailMap
        height={400}
        lines={route ? [route.path.map(([latitude, longitude]) => ({ latitude, longitude }))] : []}
        variants={alternatives.slice(1).map((a, i) => ({ id: `alt-${i}`, color: ALTERNATIVE_COLORS[i + 1], coordinates: a.path.map(([latitude, longitude]) => ({ latitude, longitude })) }))}
        markers={markers}
        pois={poiList.map((p) => ({ id: `poi-${p.id}`, latitude: p.latitude, longitude: p.longitude, name: p.name, type: p.kind }))}
        bounds={fitted?.bounds}
        center={initialCenter ?? undefined}
        zoom={12}
        showUser
        onMapPress={(c) => insert({ ...c, isOvernight: false })}
        onMarkerPress={(markerId) => {
          if (markerId.startsWith('wp-')) waypointActions(Number(markerId.slice(3)));
          const poi = poiList.find((p) => `poi-${p.id}` === markerId);
          if (poi) poiActions(poi);
        }}
      />
      <ThemedText type="small" themeColor="textSecondary">
        {waypoints.length === 0
          ? 'Tap the map to place your start.'
          : waypoints.length === 1
            ? 'Now tap where you want to go. The route follows hiking paths.'
            : preview.isFetching
              ? 'Routing…'
              : 'Tap to add points, tap a point to change it, or tap a hut to stay the night.'}
      </ThemedText>
      {preview.error && <ThemedText type="small" style={{ color: '#DC2626' }}>{describeError(preview.error)}</ThemedText>}

      <View style={styles.chips}>
        {POI_FILTERS.map((f) => (
          <Chip
            key={f.label}
            label={f.label}
            selected={poiFilters.includes(f.label)}
            onPress={() => setPoiFilters(poiFilters.includes(f.label) ? poiFilters.filter((x) => x !== f.label) : [...poiFilters, f.label])}
          />
        ))}
      </View>
      {pois.error && kinds.length > 0 && <ThemedText type="small" themeColor="textSecondary">{describeError(pois.error)}</ThemedText>}

      {route && (
        <>
          <RouteWarnings warnings={route.warnings} />
          <RouteStats route={route} units={units} />

          {waypoints.length === 2 && (
            <Section title="Other ways">
              {alternatives.length === 0 ? (
                <Button
                  icon="arrow.triangle.branch"
                  label="Find alternative routes"
                  variant="tinted"
                  loading={findAlternatives.isPending}
                  onPress={() => findAlternatives.mutate({ from: waypoints[0], to: waypoints[1] }, { onError: (e) => Alert.alert('No alternatives', describeError(e)) })}
                />
              ) : alternatives.length === 1 ? (
                <ThemedText type="small" themeColor="textSecondary">
                  This is the only sensible way between these points.
                </ThemedText>
              ) : (
                <List>
                  {alternatives.map((a, i) => (
                    <ListRow
                      key={i}
                      icon={i === 0 ? 'checkmark' : 'arrow.triangle.branch'}
                      iconColor={i === 0 ? '#1F7A4D' : ALTERNATIVE_COLORS[i]}
                      title={i === 0 ? 'Current (recommended)' : `Alternative ${i}`}
                      detail={`${formatDistance(a.stats.distanceMeters, units)} · ↑ ${formatElevation(a.stats.ascentMeters, units)} · ${formatMinutes(a.stats.durationMinutes)}`}
                      onPress={i === 0 ? undefined : () => update([waypoints[0], ...viaPointsOf(a), waypoints[1]])}
                    />
                  ))}
                </List>
              )}
            </Section>
          )}

          <RouteElevation route={route} units={units} />
          <Itinerary days={route.days} units={units} />
        </>
      )}

      {waypoints.length >= 2 && (
        <Section title={id ? 'Route' : 'Save'}>
          <Card>
            <Input icon="pencil" placeholder="Route name, e.g. Lac Blanc loop" value={routeName} onChangeText={setName} returnKeyType="done" />
            <Button
              icon="square.and.arrow.down"
              label={id ? (edited !== null || name !== null ? 'Save changes' : 'Saved') : 'Save route'}
              onPress={() => doSave()}
              loading={save.isPending}
              disabled={!!id && edited === null && name === null}
            />
            <Button icon="square.and.arrow.up" label="Export GPX" variant="secondary" onPress={exportGpx} />
          </Card>
        </Section>
      )}

      {id && (
        <List>
          <ListRow
            title="Delete route"
            destructive
            onPress={() =>
              Alert.alert('Delete this route?', undefined, [
                { text: 'Cancel', style: 'cancel' },
                { text: 'Delete', style: 'destructive', onPress: () => remove.mutate(id, { onSuccess: () => router.back() }) },
              ])
            }
          />
        </List>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});

