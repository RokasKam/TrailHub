import { Link, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { useTrip, useTripChange } from '@/api/planning';
import { useTrail } from '@/api/trails';
import { DateField, toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { TrailPicker } from '@/components/trail-picker';
import { Button, Card, Icon, Input, List, QueryState, Screen, Section } from '@/components/ui';
import { WeatherCard } from '@/components/weather-card';
import { Spacing } from '@/constants/theme';
import { formatDate } from '@/lib/format';
import type { Trip } from '@/lib/types';

/** Forecasts only reach ~16 days ahead. */
const FORECAST_HORIZON_MS = 15 * 24 * 60 * 60 * 1000;

/** DateOnly strings are calendar dates; parse them as local midnight, not UTC. */
const fromDateOnly = (s: string) => {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
};

export default function TripScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const trip = useTrip(id);
  const change = useTripChange(id);
  const [pickingFor, setPickingFor] = useState<number | null>(null);
  const [editingDay, setEditingDay] = useState<number | null>(null);
  const [item, setItem] = useState('');

  if (!trip.data) return <Screen><QueryState isLoading={trip.isLoading} error={trip.error} /></Screen>;
  const t = trip.data;
  const editable = t.status === 'Planned';

  return (
    <Screen>
      <Stack.Screen options={{ title: '' }} />
      <ThemedText type="subtitle">{t.name}</ThemedText>
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
          {formatDate(t.startDate)} – {formatDate(t.endDate)} · {t.status}
        </ThemedText>
      )}
      {change.error && <ThemedText type="small">{change.error.message}</ThemedText>}

      {t.days.map((day) => (
        <Section
          key={day.dayNumber}
          title={`Day ${day.dayNumber} · ${formatDate(day.date)}`}
          action={editable && editingDay !== day.dayNumber ? <Button size="small" icon="pencil" label="Edit" variant="secondary" onPress={() => setEditingDay(day.dayNumber)} /> : undefined}>
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

      <Section title="Checklist">
        {t.checklist.length > 0 && (
          <List>
            {t.checklist.map((c) => (
              <Pressable key={c.id} onPress={() => change.mutate({ kind: 'toggleItem', itemId: c.id, isDone: !c.isDone })} onLongPress={() => change.mutate({ kind: 'removeItem', itemId: c.id })}>
                <View style={styles.checkItem}>
                  <Icon name={c.isDone ? 'checkmark.circle.fill' : 'circle'} size={22} color={c.isDone ? '#16A34A' : '#A8A29E'} weight="regular" />
                  <ThemedText style={[{ flex: 1 }, c.isDone && styles.done]}>{c.text}</ThemedText>
                </View>
              </Pressable>
            ))}
          </List>
        )}
        <Input
          icon="plus.circle"
          placeholder="Add item (long-press an item to remove it)"
          value={item}
          onChangeText={setItem}
          returnKeyType="done"
          onSubmitEditing={() => item.trim() && change.mutate({ kind: 'addItem', text: item }, { onSuccess: () => setItem('') })}
        />
      </Section>

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
  row: { flexDirection: 'row', gap: Spacing.two },
  done: { textDecorationLine: 'line-through', opacity: 0.5 },
  inline: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  checkItem: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4, minHeight: 44 },
});
