import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useCreateTrip } from '@/api/planning';
import { useUnits } from '@/api/profile';
import { usePlaceSearch, usePlanItinerary, useSaveRoute } from '@/api/routes';
import { DateField, toDateOnly } from '@/components/date-field';
import { Itinerary, RouteElevation, RouteStats, RouteWarnings } from '@/components/route-summary';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Button, Card, Chip, Icon, IconButton, Input, List, ListRow, Screen, Section, Segmented } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { api, describeError } from '@/lib/api';
import { formatDistance } from '@/lib/format';
import type { Accommodation, Bounds, Place, PlanDifficulty, PlannedRoute, Trip } from '@/lib/types';

const DAY_MS = 86_400_000;
const MAX_DAILY_OPTIONS = [null, 12, 16, 20, 25, 30] as const;

type PickTarget = 'start' | 'finish' | 'via' | null;
const MAX_VIA = 8;

/** Smallest box around the points, so the map shows the whole hike (or everything picked so far). */
function boundsOf(points: { latitude: number; longitude: number }[]): Bounds | undefined {
  if (points.length < 2) return undefined;
  const lats = points.map((p) => p.latitude);
  const lons = points.map((p) => p.longitude);
  return { minLatitude: Math.min(...lats), minLongitude: Math.min(...lons), maxLatitude: Math.max(...lats), maxLongitude: Math.max(...lons) };
}

/** `value`, once it has stopped changing for `ms`: search as the user pauses, not on every keystroke. */
function useDebounced<T>(value: T, ms = 300) {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), ms);
    return () => clearTimeout(timer);
  }, [value, ms]);
  return settled;
}

export default function PlannerScreen() {
  const units = useUnits();
  const theme = useTheme();
  const plan = usePlanItinerary();
  const saveRoute = useSaveRoute(null);
  const createTrip = useCreateTrip();

  const [start, setStart] = useState<Place | null>(null);
  const [finish, setFinish] = useState<Place | null>(null);
  const [via, setVia] = useState<Place[]>([]);
  const [picking, setPicking] = useState<PickTarget>(null);
  const [days, setDays] = useState(4);
  const [maxDaily, setMaxDaily] = useState<number | null>(20);
  const [accommodation, setAccommodation] = useState<Accommodation>('Hut');
  const [difficulty, setDifficulty] = useState<PlanDifficulty>('Moderate');
  const [tripStart, setTripStart] = useState(() => new Date(Date.now() + 14 * DAY_MS));
  const [busy, setBusy] = useState(false);

  const result = plan.data;
  const ready = start && finish;

  const generate = () =>
    ready &&
    plan.mutate({
      start: { latitude: start.latitude, longitude: start.longitude, name: start.name, isOvernight: false },
      finish: { latitude: finish.latitude, longitude: finish.longitude, name: finish.name, isOvernight: false },
      via: via.map((v) => ({ latitude: v.latitude, longitude: v.longitude, name: v.name, isOvernight: false })),
      days,
      maxDailyDistanceKm: maxDaily,
      accommodation,
      difficulty,
    });

  const routeName = `${start?.name ?? 'Start'} → ${finish?.name ?? 'Finish'}`;

  const save = () =>
    result &&
    saveRoute.mutate(
      { name: routeName, waypoints: result.waypoints },
      { onSuccess: (r) => router.push(`/routes/${r.id}`), onError: (e) => Alert.alert("Couldn't save", describeError(e)) },
    );

  // Saves the route, then makes a trip with one day per stage and the route attached, so each trip day shows its
  // stage and can be started from the trip. Days also get the overnight stop and a note, readable without the route.
  const toTrip = async (r: PlannedRoute) => {
    setBusy(true);
    try {
      const saved = await saveRoute.mutateAsync({ name: routeName, waypoints: r.waypoints });
      const end = new Date(tripStart.getTime() + (r.days.length - 1) * DAY_MS);
      const trip = await createTrip.mutateAsync({ name: routeName, startDate: toDateOnly(tripStart), endDate: toDateOnly(end) });
      await api<Trip>(`/api/trips/${trip.id}/route`, { method: 'PUT', body: { routeId: saved.id } });
      for (const d of r.days) {
        const note = `${formatDistance(d.stats.distanceMeters, units)} · ↑ ${Math.round(d.stats.ascentMeters)} m · ~${Math.round(d.stats.durationMinutes / 60)} h walking`;
        await api<Trip>(`/api/trips/${trip.id}/days/${d.day}`, { method: 'PUT', body: { notes: note, overnight: d.overnight?.name ?? null } });
      }
      router.push(`/trips/${trip.id}`);
    } catch (e) {
      Alert.alert("Couldn't create the trip", describeError(e as Error));
    } finally {
      setBusy(false);
    }
  };

  const mapBounds = useMemo(
    () =>
      boundsOf(
        result
          ? result.path.map(([latitude, longitude]) => ({ latitude, longitude }))
          : [start, ...via, finish].filter((p): p is Place => p !== null),
      ),
    [result, start, via, finish],
  );

  const overnightMarkers = (result?.waypoints ?? [])
    .map((w, i) => ({ w, i }))
    .filter(({ w }) => w.isOvernight)
    .map(({ w }, n) => ({ id: `night-${n}`, latitude: w.latitude, longitude: w.longitude, label: `Night ${n + 1}`, color: '#7C3AED' }));

  return (
    <Screen>
      <Section title="Where">
        <Card>
          <PlaceField label="Start" icon="flag.fill" color="#16A34A" value={start} onChange={setStart} picking={picking === 'start'} onPick={() => setPicking(picking === 'start' ? null : 'start')} near={finish} />
          <PlaceField label="Finish" icon="flag.checkered" color="#DC2626" value={finish} onChange={setFinish} picking={picking === 'finish'} onPick={() => setPicking(picking === 'finish' ? null : 'finish')} near={start} />
        </Card>

        <Card>
          <ThemedText type="smallBold">Pass through (optional)</ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            Routes follow paths on the map and tend to take valleys. Add huts, passes or villages in order to keep it where you want to hike.
          </ThemedText>
          {via.map((v, i) => (
            <View key={`${v.name}-${i}`} style={styles.row}>
              <ThemedText type="number" style={{ fontSize: 15, width: 20 }}>
                {i + 1}
              </ThemedText>
              <View style={{ flex: 1 }}>
                <ThemedText type="smallBold">{v.name}</ThemedText>
                {v.region && (
                  <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
                    {v.region}
                  </ThemedText>
                )}
              </View>
              <IconButton icon="xmark" label={`Remove ${v.name}`} onPress={() => setVia(via.filter((_, j) => j !== i))} />
            </View>
          ))}
          {via.length < MAX_VIA && (
            <PlaceField
              key={via.length}
              label="Stop"
              icon="flag.fill"
              color="#0F172A"
              value={null}
              onChange={(p) => p && setVia([...via, p])}
              picking={picking === 'via'}
              onPick={() => setPicking(picking === 'via' ? null : 'via')}
              near={via[via.length - 1] ?? start}
            />
          )}
        </Card>
        {(picking || result || start || finish) && (
          <TrailMap
            height={picking ? 320 : 260}
            lines={result ? [result.path.map(([latitude, longitude]) => ({ latitude, longitude }))] : []}
            markers={[
              ...(start ? [{ id: 'start', latitude: start.latitude, longitude: start.longitude, label: start.name, color: '#16A34A' }] : []),
              ...(finish ? [{ id: 'finish', latitude: finish.latitude, longitude: finish.longitude, label: finish.name, color: '#DC2626' }] : []),
              ...(result ? [] : via.map((v, i) => ({ id: `via-${i}`, latitude: v.latitude, longitude: v.longitude, label: v.name, color: '#0F172A' }))),
              ...overnightMarkers,
            ]}
            bounds={mapBounds}
            center={start ?? finish ?? { latitude: 45.9237, longitude: 6.8694 }}
            zoom={11}
            onMapPress={
              picking
                ? (c) => {
                    const name = picking === 'start' ? 'Start' : picking === 'finish' ? 'Finish' : `Stop ${via.length + 1}`;
                    const place = { name, region: 'Picked on the map', ...c };
                    if (picking === 'via') setVia([...via, place]);
                    else (picking === 'start' ? setStart : setFinish)(place);
                    setPicking(null);
                  }
                : undefined
            }
          />
        )}
        {picking && (
          <ThemedText type="small" themeColor="textSecondary">
            Tap the map to set {picking === 'via' ? 'a stop on the way' : `the ${picking}`}.
          </ThemedText>
        )}
      </Section>

      <Section title="How">
        <Card>
          <View style={styles.row}>
            <View style={{ flex: 1 }}>
              <ThemedText type="smallBold">Days</ThemedText>
              <ThemedText type="small" themeColor="textSecondary">
                Walking days, start to finish
              </ThemedText>
            </View>
            <IconButton icon="minus" label="Fewer days" tinted onPress={() => setDays(Math.max(1, days - 1))} />
            <ThemedText type="number" style={styles.count}>
              {days}
            </ThemedText>
            <IconButton icon="plus" label="More days" tinted onPress={() => setDays(Math.min(30, days + 1))} />
          </View>

          <ThemedText type="smallBold">Maximum per day</ThemedText>
          <View style={styles.chips}>
            {MAX_DAILY_OPTIONS.map((km) => (
              <Chip key={km ?? 'none'} label={km ? `${km} km` : 'No limit'} selected={maxDaily === km} onPress={() => setMaxDaily(km)} />
            ))}
          </View>

          <ThemedText type="smallBold">Sleep in</ThemedText>
          <Segmented
            value={accommodation}
            onChange={setAccommodation}
            options={[
              { value: 'Tent', label: 'Tent', icon: 'tent' },
              { value: 'Hut', label: 'Hut', icon: 'house.lodge' },
              { value: 'Hotel', label: 'Hotel', icon: 'bed.double' },
            ]}
          />

          <ThemedText type="smallBold">Difficulty</ThemedText>
          <Segmented
            value={difficulty}
            onChange={setDifficulty}
            options={[
              { value: 'Easy', label: 'Easy' },
              { value: 'Moderate', label: 'Moderate' },
              { value: 'Hard', label: 'Hard' },
              { value: 'Expert', label: 'Expert' },
            ]}
          />
          <ThemedText type="small" themeColor="textSecondary">
            {DIFFICULTY_HINTS[difficulty]}
          </ThemedText>
        </Card>
      </Section>

      <Button size="large" icon="sparkles" label={result ? 'Plan again' : 'Plan my hike'} onPress={generate} loading={plan.isPending} disabled={!ready} />
      {plan.isPending && (
        <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
          Finding paths and places to stay. Long routes can take half a minute.
        </ThemedText>
      )}
      {plan.error && <ThemedText type="small" style={{ color: theme.danger }}>{describeError(plan.error)}</ThemedText>}

      {result && (
        <>
          <Itinerary days={result.days} units={units} />
          <RouteWarnings warnings={result.warnings} />
          <RouteStats route={result} units={units} />
          <RouteElevation route={result} units={units} />

          <Section title="Next">
            <Card>
              <Button icon="square.and.arrow.down" label="Save route" onPress={save} loading={saveRoute.isPending} />
              <DateField label="First day" value={tripStart} onChange={setTripStart} minimumDate={new Date()} />
              <Button icon="calendar.badge.plus" label="Create trip from this" variant="secondary" onPress={() => void toTrip(result)} loading={busy} />
              <ThemedText type="small" themeColor="textSecondary">
                Saves the route and makes a trip with one day per stage, each with its overnight stop and a Start button. Share the trip with a group and they get the route too.
              </ThemedText>
            </Card>
          </Section>
        </>
      )}
    </Screen>
  );
}

const DIFFICULTY_HINTS: Record<PlanDifficulty, string> = {
  Easy: 'About 4½ hours of walking a day, well-made paths (up to T2).',
  Moderate: 'About 6 hours a day; some steep or exposed sections are fine (up to T3).',
  Hard: 'About 7½ hours a day on demanding mountain paths (up to T4).',
  Expert: 'About 9 hours a day, alpine terrain allowed.',
};

/** Search a place by name, or pick it on the map. */
function PlaceField({
  label,
  icon,
  color,
  value,
  onChange,
  picking,
  onPick,
  near,
}: {
  label: string;
  icon: 'flag.fill' | 'flag.checkered';
  color: string;
  value: Place | null;
  onChange: (p: Place | null) => void;
  picking: boolean;
  onPick: () => void;
  near: Place | null;
}) {
  const [text, setText] = useState('');
  const query = useDebounced(text);
  const results = usePlaceSearch(value ? '' : query, near);

  if (value) {
    return (
      <Pressable onPress={() => onChange(null)} accessibilityRole="button" accessibilityLabel={`Change ${label.toLowerCase()}`}>
        <View style={styles.row}>
          <Icon name={icon} size={16} color={color} />
          <View style={{ flex: 1 }}>
            <ThemedText type="caption" themeColor="textSecondary">
              {label.toUpperCase()}
            </ThemedText>
            <ThemedText type="smallBold">{value.name}</ThemedText>
            {value.region && (
              <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
                {value.region}
              </ThemedText>
            )}
          </View>
          <Icon name="xmark.circle.fill" size={18} color="#9CA3AF" />
        </View>
      </Pressable>
    );
  }

  return (
    <View style={{ gap: Spacing.two }}>
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Input icon="magnifyingglass" placeholder={`${label}: town, hut or peak`} value={text} onChangeText={setText} autoCorrect={false} />
        </View>
        <IconButton icon="mappin.and.ellipse" label={`Pick ${label.toLowerCase()} on the map`} tinted={!picking} onPress={onPick} />
      </View>
      {!!results.data?.length && (
        <List>
          {results.data.slice(0, 5).map((p) => (
            <ListRow
              key={`${p.name}-${p.latitude}`}
              icon="mappin"
              iconColor={color}
              title={p.name}
              detail={p.region ?? undefined}
              onPress={() => {
                onChange(p);
                setText('');
              }}
            />
          ))}
        </List>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  count: { minWidth: 32, textAlign: 'center' },
});
