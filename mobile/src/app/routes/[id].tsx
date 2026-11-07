import * as Location from 'expo-location';
import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActionSheetIOS, Alert, Platform, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { useGroups } from '@/api/planning';
import { approxMeters, downloadRouteGpx, useAlternatives, useCopyRoute, useDeleteRoute, usePoisAlong, usePublishRoute, useUnpublishRoute, suggestRouteName, useRoute, useRoutePreview, useSaveRoute, useShareRoute } from '@/api/routes';
import { HikeRoute } from '@/components/route-hike';
import { Itinerary, POI_LABELS, RouteElevation, RouteStats, RouteWarnings } from '@/components/route-summary';
import { ThemedText } from '@/components/themed-text';
import { TrailMap, type MapMarker } from '@/components/trail-map';
import { Button, Card, Chip, Icon, IconButton, Input, List, ListRow, QueryState, Screen, Section, Segmented } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { describeError } from '@/lib/api';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import { shareTextFile } from '@/lib/share-file';
import type { Bounds, PlanDifficulty, PlannedRoute, Poi, PoiKind, RouteWaypoint } from '@/lib/types';

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
  const copy = useCopyRoute();

  // Edits live here until saved; until the first edit, the saved route is shown. Each edit pushes the previous
  // waypoints onto `history`, so undo steps back through moves, removals and overnight changes, not just added points.
  const [edited, setEdited] = useState<RouteWaypoint[] | null>(null);
  const [history, setHistory] = useState<(RouteWaypoint[] | null)[]>([]);
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
  // Someone shared this route with your group: you can view, follow, export and copy it, not change it.
  const readOnly = !!id && saved.data?.isOwner === false;

  // With unsaved changes, back asks first (and swipe-back is off, so it can't skip the question).
  const dirty = edited !== null || (name !== null && name !== (saved.data?.name ?? ''));
  const back = () =>
    dirty
      ? Alert.alert('Discard changes?', id ? 'Your changes to this route are not saved.' : 'This route is not saved.', [
          { text: 'Keep editing', style: 'cancel' },
          { text: 'Discard', style: 'destructive', onPress: () => router.back() },
        ])
      : router.back();

  const update = (next: RouteWaypoint[]) => {
    findAlternatives.reset();
    setHistory([...history, edited]);
    setEdited(next);
  };
  const undo = () => {
    if (history.length === 0) return;
    findAlternatives.reset();
    setEdited(history[history.length - 1]);
    setHistory(history.slice(0, -1));
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
  const doSave = async (then?: (id: string) => void) => {
    const finalName = routeName.trim() || (await suggestRouteName(waypoints)) || `Route ${new Date().toLocaleDateString()}`;
    save.mutate(
      { name: finalName, description: saved.data?.description ?? null, waypoints },
      {
        onSuccess: (r) => {
          setEdited(null);
          setHistory([]);
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
    else void doSave((savedId) => void run(savedId));
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
          gestureEnabled: !dirty,
          headerBackVisible: !dirty,
          headerLeft: dirty ? () => <IconButton icon="chevron.left" label="Back" onPress={back} /> : undefined,
          headerRight: () => (history.length > 0 && !readOnly ? <IconButton icon="arrow.uturn.backward" label="Undo" onPress={undo} /> : null),
        }}
      />

      <TrailMap
        height={400}
        lines={route ? [route.path.map(([latitude, longitude]) => ({ latitude, longitude }))] : []}
        variants={alternatives.slice(1).map((a, i) => ({ id: `alt-${i}`, color: ALTERNATIVE_COLORS[i + 1], coordinates: a.path.map(([latitude, longitude]) => ({ latitude, longitude })) }))}
        markers={readOnly ? markers : []}
        draggableMarkers={readOnly ? [] : markers}
        onMarkerDragEnd={(markerId, c) => {
          const i = Number(markerId.slice(3));
          // A dragged point is a new place: drop any hut name it had, keep whether the night is spent there.
          update(waypoints.map((w, j) => (j === i ? { ...w, ...c, name: null, kind: null } : w)));
        }}
        pois={poiList.map((p) => ({ id: `poi-${p.id}`, latitude: p.latitude, longitude: p.longitude, name: p.name, type: p.kind }))}
        bounds={fitted?.bounds}
        center={initialCenter ?? undefined}
        zoom={12}
        showUser
        onMapPress={readOnly ? undefined : (c) => insert({ ...c, isOvernight: false })}
        onMarkerPress={(markerId) => {
          if (readOnly) return;
          if (markerId.startsWith('wp-')) waypointActions(Number(markerId.slice(3)));
          const poi = poiList.find((p) => `poi-${p.id}` === markerId);
          if (poi) poiActions(poi);
        }}
      />
      {readOnly ? (
        <Card style={styles.inline}>
          <Icon name="person.3.fill" size={16} color="#2563EB" />
          <ThemedText type="small" style={{ flex: 1 }}>
            Shared with {saved.data?.sharedWith?.map((g) => g.name).join(', ') || 'your group'}. Copy it to change it.
          </ThemedText>
          <Button
            size="small"
            variant="tinted"
            icon="doc.on.doc"
            label="Copy"
            loading={copy.isPending}
            onPress={() => copy.mutate(id!, { onSuccess: (r) => router.replace(`/routes/${r.id}`), onError: (e) => Alert.alert("Couldn't copy", describeError(e)) })}
          />
        </Card>
      ) : (
      <ThemedText type="small" themeColor="textSecondary">
        {waypoints.length === 0
          ? 'Tap the map to place your start.'
          : waypoints.length === 1
            ? 'Now tap where you want to go. The route follows hiking paths.'
            : preview.isFetching
              ? 'Routing…'
              : 'Tap to add points, hold and drag a point to move it, tap it for options, or tap a hut to stay the night.'}
      </ThemedText>
      )}
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

          {id && edited === null && (
            <Section title="Hike it">
              <HikeRoute route={route} units={units} />
            </Section>
          )}

          {waypoints.length === 2 && !readOnly && (
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

      {id && !readOnly && saved.data && edited === null && (
        <>
          <ShareRoute route={saved.data} />
          <PublishRoute route={saved.data} />
        </>
      )}

      {readOnly && (
        <List>
          <ListRow icon="square.and.arrow.up" iconColor="#64748B" title="Export GPX" onPress={exportGpx} />
        </List>
      )}

      {waypoints.length >= 2 && !readOnly && (
        <Section title={id ? 'Route' : 'Save'}>
          <Card>
            <Input icon="pencil" placeholder="Route name, e.g. Lac Blanc loop" value={routeName} onChangeText={setName} returnKeyType="done" />
            <Button
              icon="square.and.arrow.down"
              label={id ? (edited !== null || name !== null ? 'Save changes' : 'Saved') : 'Save route'}
              onPress={() => void doSave()}
              loading={save.isPending}
              disabled={!!id && edited === null && name === null}
            />
            <Button icon="square.and.arrow.up" label="Export GPX" variant="secondary" onPress={exportGpx} />
          </Card>
        </Section>
      )}

      {id && !readOnly && (
        <List>
          <ListRow
            title="Delete route"
            destructive
            onPress={() =>
              Alert.alert('Delete this route?', undefined, [
                { text: 'Cancel', style: 'cancel' },
                {
                text: 'Delete',
                style: 'destructive',
                onPress: () => remove.mutate(id, { onSuccess: () => router.back() }),
              },
              ])
            }
          />
        </List>
      )}
    </Screen>
  );
}

/** Which of your groups can see, follow and copy this route. */
function ShareRoute({ route }: { route: PlannedRoute }) {
  const groups = useGroups();
  const share = useShareRoute(route.id!);
  const shared = new Set(route.sharedWith?.map((g) => g.id));
  if (!groups.data?.length) return null;
  return (
    <Section title="Share with a group">
      <Card>
        <View style={styles.chips}>
          {groups.data.map((g) => (
            <Chip
              key={g.id}
              icon={shared.has(g.id) ? 'checkmark' : 'person.3'}
              label={g.name}
              selected={shared.has(g.id)}
              onPress={() => share.mutate({ groupId: g.id, share: !shared.has(g.id) }, { onError: (e) => Alert.alert("Couldn't share", describeError(e)) })}
            />
          ))}
        </View>
        <ThemedText type="small" themeColor="textSecondary">
          Members can see the route, follow it on a hike, export it and copy it. Only you can change it.
        </ThemedText>
      </Card>
    </Section>
  );
}

const DIFFICULTIES: PlanDifficulty[] = ['Easy', 'Moderate', 'Hard', 'Expert'];

/** Publishes the route to Discover as a public trail; published again, it updates that trail. */
function PublishRoute({ route }: { route: PlannedRoute }) {
  const publish = usePublishRoute(route.id!);
  const unpublish = useUnpublishRoute(route.id!);
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState(route.name ?? '');
  const [description, setDescription] = useState(route.description ?? '');
  const [region, setRegion] = useState('');
  const [difficulty, setDifficulty] = useState<PlanDifficulty>(route.stats.maxSacScale >= 4 ? 'Hard' : route.stats.durationMinutes > 360 ? 'Hard' : 'Moderate');
  const blocker = !route.snapped ? 'Only routes that follow paths can be published.' : route.stats.distanceMeters < 1000 ? 'Routes shorter than 1 km can’t be published.' : null;

  return (
    <Section title="Discover">
      <Card>
        {route.publishedAtUtc ? (
          <View style={styles.inline}>
            <Icon name="globe.europe.africa.fill" size={16} color="#1F7A4D" />
            <ThemedText type="small" style={{ flex: 1 }}>
              Published {new Date(route.publishedAtUtc).toLocaleDateString()}. Edits here don’t change the public trail until you publish again.
            </ThemedText>
          </View>
        ) : (
          <ThemedText type="small" themeColor="textSecondary">
            Publish this route as a public trail so other hikers can find it in Discover, review it and add photos.
          </ThemedText>
        )}
        {blocker ? (
          <ThemedText type="small" themeColor="textSecondary">{blocker}</ThemedText>
        ) : open ? (
          <>
            <Input icon="textformat" placeholder="Trail name" value={title} onChangeText={setTitle} />
            <Input icon="mappin.and.ellipse" placeholder="Region, e.g. Chamonix valley" value={region} onChangeText={setRegion} />
            <Input placeholder="Description (optional)" value={description} onChangeText={setDescription} multiline />
            <Segmented value={difficulty} onChange={setDifficulty} options={DIFFICULTIES.map((d) => ({ value: d, label: d }))} />
            <Button
              icon="globe"
              label={route.publishedAtUtc ? 'Publish again' : 'Publish to Discover'}
              loading={publish.isPending}
              disabled={!title.trim() || !region.trim()}
              onPress={() =>
                publish.mutate(
                  { name: title.trim(), description: description.trim() || null, region: region.trim(), difficulty },
                  { onSuccess: () => setOpen(false), onError: (e) => Alert.alert("Couldn't publish", describeError(e)) },
                )
              }
            />
          </>
        ) : (
          <View style={styles.row}>
            <View style={{ flex: 1 }}>
              <Button icon="globe" variant="tinted" label={route.publishedAtUtc ? 'Publish again' : 'Publish'} onPress={() => setOpen(true)} />
            </View>
            {route.publishedAtUtc && (
              <View style={{ flex: 1 }}>
                <Button icon="map" variant="secondary" label="View trail" onPress={() => router.push(`/trail/${route.id}`)} />
              </View>
            )}
          </View>
        )}
        {route.publishedAtUtc && !open && (
          <View style={styles.row}>
            <Button
              size="small"
              variant="secondary"
              icon="eye.slash"
              label="Unpublish"
              loading={unpublish.isPending}
              onPress={() =>
                Alert.alert('Take it out of Discover?', 'Its reviews and photos are kept if you publish it again.', [
                  { text: 'Cancel', style: 'cancel' },
                  { text: 'Unpublish', style: 'destructive', onPress: () => unpublish.mutate(undefined, { onError: (e) => Alert.alert("Couldn't unpublish", describeError(e)) }) },
                ])
              }
            />
          </View>
        )}
      </Card>
    </Section>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  inline: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  row: { flexDirection: 'row', gap: Spacing.two },
});

