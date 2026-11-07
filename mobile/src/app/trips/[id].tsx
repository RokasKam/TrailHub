import { Link, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';

import { useTripLiveUpdates } from '@/api/live-location';
import { useGroups, useTrip, useTripChange } from '@/api/planning';
import { useUnits } from '@/api/profile';
import { shareRouteWithGroup, useRoute, useRoutes } from '@/api/routes';
import { useTrail } from '@/api/trails';
import { useSession } from '@/auth/session';
import { DateField, toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { TrailPicker } from '@/components/trail-picker';
import { useStartRouteHike } from '@/components/route-hike';
import { TrailMap } from '@/components/trail-map';
import { TripChecklist } from '@/components/trip-checklist';
import { Button, Card, Chip, Icon, Input, List, ListRow, QueryState, Screen, Section } from '@/components/ui';
import { WeatherCard } from '@/components/weather-card';
import { Spacing } from '@/constants/theme';
import { formatDate, formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import type { PlannedRoute, Trip, Units } from '@/lib/types';

/** Forecasts only reach ~16 days ahead. */
const FORECAST_HORIZON_MS = 15 * 24 * 60 * 60 * 1000;

/** DateOnly strings are calendar dates; parse them as local midnight, not UTC. */
const fromDateOnly = (s: string) => {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
};

export default function TripScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { user } = useSession();
  const trip = useTrip(id);
  const change = useTripChange(id);
  useTripLiveUpdates(id, trip.data?.groupId != null); // others' claims and ticks show up within seconds
  const units = useUnits();
  const plannedRoute = useRoute(trip.data?.routeId ?? null);
  const startRouteHike = useStartRouteHike();
  const [pickingFor, setPickingFor] = useState<number | null>(null);
  const [editingDay, setEditingDay] = useState<number | null>(null);

  if (!trip.data) return <Screen><QueryState isLoading={trip.isLoading} error={trip.error} /></Screen>;
  const t = trip.data;
  // The plan is the owner's; people the trip is shared with see it read-only and use the checklist.
  const editable = t.status === 'Planned' && t.isOwner;
  const planner = t.members.find((m) => m.isOwner)?.displayName;

  return (
    <Screen>
      <Stack.Screen options={{ title: '' }} />
      <View>
        <ThemedText type="subtitle">{t.name}</ThemedText>
        {!t.isOwner && (
          <ThemedText type="small" themeColor="textSecondary">
            Planned by {planner ?? 'a group member'} · {t.groupName}
          </ThemedText>
        )}
      </View>
      {t.isOwner && t.status === 'Planned' && <ShareCard trip={t} change={change} />}
      <TripRoute trip={t} route={plannedRoute.data} routeError={plannedRoute.error} change={change} units={units} />
      {editable ? (
        <Card>
          <DateField
            label="Start"
            value={fromDateOnly(t.startDate)}
            onChange={(d) => {
              // Shift the whole trip, keeping its length.
              const end = new Date(d.getTime() + (t.dayCount - 1) * 86_400_000);
              change.mutate({ kind: 'reschedule', startDate: toDateOnly(d), endDate: toDateOnly(end) });
            }}
          />
          <DateField
            label="End"
            value={fromDateOnly(t.endDate)}
            minimumDate={fromDateOnly(t.startDate)}
            onChange={(d) => change.mutate({ kind: 'reschedule', startDate: t.startDate, endDate: toDateOnly(d) })}
          />
          <ThemedText type="small" themeColor="textSecondary">
            {t.dayCount} day{t.dayCount > 1 ? 's' : ''} · shortening keeps the plans of the remaining days
          </ThemedText>
        </Card>
      ) : (
        <ThemedText type="small" themeColor="textSecondary">
          {formatDate(t.startDate)} – {formatDate(t.endDate)} · {t.dayCount} day{t.dayCount > 1 ? 's' : ''}
          {t.status !== 'Planned' && ` · ${t.status}`}
        </ThemedText>
      )}
      {change.error && <ThemedText type="small">{change.error.message}</ThemedText>}

      {t.days.map((day) => (
        <Section
          key={day.dayNumber}
          title={`Day ${day.dayNumber} · ${formatDate(day.date)}`}
          action={editable && editingDay !== day.dayNumber ? <Button size="small" icon="pencil" label="Edit" variant="secondary" onPress={() => setEditingDay(day.dayNumber)} /> : undefined}>
          {plannedRoute.data?.days[day.dayNumber - 1] && (
            <RouteStage route={plannedRoute.data} day={day.dayNumber} units={units} onStart={() => startRouteHike(plannedRoute.data!, day.dayNumber)} />
          )}
          {day.trailIds.map((trailId) => (
            <DayTrail
              key={trailId}
              trailId={trailId}
              date={day.date}
              onRemove={editable ? () => change.mutate({ kind: 'removeTrail', day: day.dayNumber, trailId }) : undefined}
            />
          ))}
          {editingDay === day.dayNumber ? (
            <DayEditor
              day={day}
              saving={change.isPending}
              onCancel={() => setEditingDay(null)}
              onSave={(notes, overnight) =>
                change.mutate({ kind: 'updateDay', day: day.dayNumber, notes, overnight }, { onSuccess: () => setEditingDay(null) })
              }
            />
          ) : (
            <>
              {day.overnight && (
                <View style={styles.inline}>
                  <Icon name="bed.double.fill" size={14} color="#7C3AED" />
                  <ThemedText type="small">{day.overnight}</ThemedText>
                </View>
              )}
              {day.notes && <ThemedText type="small" themeColor="textSecondary">{day.notes}</ThemedText>}
            </>
          )}
          {editable && <Button icon="plus" label="Add trail" variant="tinted" onPress={() => setPickingFor(day.dayNumber)} />}
        </Section>
      ))}

      <TripChecklist trip={t} myId={user?.id} change={change} />

      {editable && (
        <View style={styles.row}>
          <View style={{ flex: 1 }}>
            <Button label="Mark completed" variant="secondary" onPress={() => change.mutate({ kind: 'complete' })} />
          </View>
          <View style={{ flex: 1 }}>
            <Button label="Cancel trip" variant="danger" onPress={() => change.mutate({ kind: 'cancel' })} />
          </View>
        </View>
      )}

      <TrailPicker
        visible={pickingFor != null}
        title={pickingFor != null ? `Add to day ${pickingFor}` : undefined}
        onClose={() => setPickingFor(null)}
        onSelect={(trail) => change.mutate({ kind: 'addTrail', day: pickingFor!, trailId: trail.id }, { onSettled: () => setPickingFor(null) })}
      />
    </Screen>
  );
}

/** Who can see this trip: private, or one of my groups. Changing it is the owner's call. */
function ShareCard({ trip, change }: { trip: Trip; change: ReturnType<typeof useTripChange> }) {
  const groups = useGroups();
  const [open, setOpen] = useState(false);
  const share = (groupId: string | null) => {
    const apply = () =>
      change.mutate(
        { kind: 'share', groupId },
        {
          onSuccess: () => {
            setOpen(false);
            // Members should see the trip's route too. Only the route's author can share it, so a failure is fine.
            if (groupId && trip.routeId) void shareRouteWithGroup(trip.routeId, groupId).catch(() => undefined);
          },
          onError: (e) => Alert.alert("Couldn't share", e.message),
        },
      );
    // Leaving a group drops other people's claims and ticks, so confirm first.
    if (trip.groupId && groupId !== trip.groupId) {
      Alert.alert(groupId ? 'Move to another group?' : 'Make this trip private?', "Other people's claims and ticks will be cleared.", [
        { text: 'Cancel', style: 'cancel' },
        { text: groupId ? 'Move' : 'Make private', style: 'destructive', onPress: apply },
      ]);
    } else {
      apply();
    }
  };

  return (
    <Card>
      <View style={styles.inline}>
        <Icon name={trip.groupId ? 'person.3.fill' : 'lock.fill'} size={16} color={trip.groupId ? '#2563EB' : '#64748B'} />
        <View style={{ flex: 1 }}>
          <ThemedText type="smallBold">{trip.groupId ? `Shared with ${trip.groupName ?? 'your group'}` : 'Private trip'}</ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            {trip.groupId ? `${trip.members.length} member${trip.members.length === 1 ? '' : 's'} can see it and split the gear` : 'Share it with a group to split the gear'}
          </ThemedText>
        </View>
        <Button size="small" variant="tinted" label={open ? 'Done' : trip.groupId ? 'Change' : 'Share'} onPress={() => setOpen(!open)} />
      </View>
      {open && (
        <View style={styles.chips}>
          {groups.data?.length === 0 && (
            <ThemedText type="small" themeColor="textSecondary">
              {"You're not in any groups yet. Create or join one under Profile → Groups."}
            </ThemedText>
          )}
          {groups.data?.map((g) => (
            <Chip key={g.id} icon="person.3" label={g.name} selected={trip.groupId === g.id} onPress={() => share(g.id)} />
          ))}
          {trip.groupId && <Chip icon="lock" label="Private" onPress={() => share(null)} />}
        </View>
      )}
    </Card>
  );
}

/** The trip's planned route: map and totals, or (for the owner) a way to attach one of their routes. */
function TripRoute({ trip, route, routeError, change, units }: { trip: Trip; route?: PlannedRoute; routeError: Error | null; change: ReturnType<typeof useTripChange>; units: Units }) {
  const routes = useRoutes();
  const [picking, setPicking] = useState(false);
  const canEdit = trip.isOwner && trip.status === 'Planned';

  const attach = (routeId: string | null) =>
    change.mutate(
      { kind: 'attachRoute', routeId },
      {
        onSuccess: () => {
          setPicking(false);
          if (routeId && trip.groupId) void shareRouteWithGroup(routeId, trip.groupId).catch(() => undefined);
        },
        onError: (e) => Alert.alert("Couldn't attach the route", e.message),
      },
    );

  if (!trip.routeId && !canEdit) return null;

  return (
    <Section
      title="Route"
      action={canEdit ? <Button size="small" variant="secondary" label={picking ? 'Done' : trip.routeId ? 'Change' : 'Attach'} onPress={() => setPicking(!picking)} /> : undefined}>
      {trip.routeId && route && (
        <Card>
          <TrailMap height={200} lines={[route.path.map(([latitude, longitude]) => ({ latitude, longitude }))]} bounds={boundsOf(route)} />
          <Link href={`/routes/${route.id}`}>
            <ThemedText type="headline">{route.name}</ThemedText>
          </Link>
          <ThemedText type="small" themeColor="textSecondary">
            {formatDistance(route.stats.distanceMeters, units)} · ↑ {formatElevation(route.stats.ascentMeters, units)} · {route.days.length} day{route.days.length > 1 ? 's' : ''}
            {route.days.length !== trip.dayCount && ` · the trip has ${trip.dayCount} day${trip.dayCount > 1 ? 's' : ''}`}
          </ThemedText>
        </Card>
      )}
      {trip.routeId && !route && routeError && (
        <ThemedText type="small" themeColor="textSecondary">
          {"The planned route isn't shared with you. Ask the trip's planner to share it with the group."}
        </ThemedText>
      )}
      {!trip.routeId && !picking && (
        <ThemedText type="small" themeColor="textSecondary">
          Attach a route you drew or planned: each trip day shows its stage, and you can start a day from here.
        </ThemedText>
      )}
      {picking && (
        <List>
          {routes.data?.filter((r) => r.isOwner).length === 0 && <ListRow title="No routes yet. Draw or plan one in the Plan tab." />}
          {routes.data
            ?.filter((r) => r.isOwner)
            .map((r) => (
              <ListRow
                key={r.id}
                icon={r.id === trip.routeId ? 'checkmark' : r.dayCount > 1 ? 'calendar' : 'point.topleft.down.to.point.bottomright.curvepath'}
                iconColor={r.id === trip.routeId ? '#1F7A4D' : '#7C3AED'}
                title={r.name}
                detail={`${formatDistance(r.distanceMeters, units)} · ${r.dayCount} day${r.dayCount > 1 ? 's' : ''}`}
                onPress={() => attach(r.id)}
              />
            ))}
          {trip.routeId && <ListRow title="Remove route" destructive onPress={() => attach(null)} />}
        </List>
      )}
    </Section>
  );
}

function boundsOf(route: PlannedRoute) {
  const lats = route.path.map((p) => p[0]);
  const lons = route.path.map((p) => p[1]);
  return { minLatitude: Math.min(...lats), minLongitude: Math.min(...lons), maxLatitude: Math.max(...lats), maxLongitude: Math.max(...lons) };
}

/** One day's stage of the trip's route, with a button to start hiking it. */
function RouteStage({ route, day, units, onStart }: { route: PlannedRoute; day: number; units: Units; onStart: () => void }) {
  const stage = route.days[day - 1];
  return (
    <Card style={styles.stage}>
      <Icon name="point.topleft.down.to.point.bottomright.curvepath" size={16} color="#7C3AED" />
      <ThemedText type="small" style={{ flex: 1 }}>
        {formatDistance(stage.stats.distanceMeters, units)} · ↑ {formatElevation(stage.stats.ascentMeters, units)} · {formatMinutes(stage.stats.durationMinutes)}
        {stage.overnight?.name ? ` · night at ${stage.overnight.name}` : ''}
      </ThemedText>
      <Button size="small" variant="tinted" icon="figure.hiking" label="Start" onPress={onStart} />
    </Card>
  );
}

function DayEditor({ day, saving, onSave, onCancel }: { day: Trip['days'][number]; saving: boolean; onSave: (notes: string | null, overnight: string | null) => void; onCancel: () => void }) {
  const [notes, setNotes] = useState(day.notes ?? '');
  const [overnight, setOvernight] = useState(day.overnight ?? '');
  return (
    <Card>
      <Input placeholder="Overnight (hut, campsite, town)" value={overnight} onChangeText={setOvernight} />
      <Input placeholder="Notes" value={notes} onChangeText={setNotes} multiline />
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button label="Cancel" variant="secondary" onPress={onCancel} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Save" loading={saving} onPress={() => onSave(notes.trim() || null, overnight.trim() || null)} />
        </View>
      </View>
    </Card>
  );
}

/** A planned trail, with that day's forecast at its trailhead when the date is in range. */
function DayTrail({ trailId, date, onRemove }: { trailId: string; date: string; onRemove?: () => void }) {
  const trail = useTrail(trailId);
  const [now] = useState(() => Date.now());
  const inRange = Date.parse(date) - now < FORECAST_HORIZON_MS && Date.parse(date) >= now - 86_400_000;
  return (
    <Card>
      <Link href={`/trail/${trailId}`}>
        <ThemedText type="headline">{trail.data?.name ?? 'Loading trail…'}</ThemedText>
      </Link>
      {trail.data && inRange && (
        <WeatherCard latitude={trail.data.start.latitude} longitude={trail.data.start.longitude} elevation={trail.data.maxElevationMeters} from={date} to={date} days={1} />
      )}
      {onRemove && <Button size="small" icon="minus.circle" label="Remove" variant="secondary" onPress={onRemove} />}
    </Card>
  );
}

const styles = StyleSheet.create({
  stage: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  row: { flexDirection: 'row', gap: Spacing.two },
  inline: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
