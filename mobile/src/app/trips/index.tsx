import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useCreateTrip, useGroups, useTrips } from '@/api/planning';
import { DateField, toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { TripList } from '@/components/trip-list';
import { Button, Card, Chip, Input, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';

const DAY_MS = 24 * 60 * 60 * 1000;
const MAX_DAYS = 30;

export default function TripsScreen() {
  // Opened from a group's page, the new trip is shared with that group by default.
  const params = useLocalSearchParams<{ groupId?: string }>();
  const trips = useTrips();
  const groups = useGroups();
  const create = useCreateTrip();
  const [groupId, setGroupId] = useState<string | null>(params.groupId ?? null);
  const [today] = useState(() => new Date());
  const [name, setName] = useState('');
  const [start, setStart] = useState(() => new Date(Date.now() + 7 * DAY_MS));
  const [end, setEnd] = useState(() => new Date(Date.now() + 9 * DAY_MS));

  const days = Math.round((Date.parse(toDateOnly(end)) - Date.parse(toDateOnly(start))) / DAY_MS) + 1;
  const datesValid = days >= 1 && days <= MAX_DAYS;

  const changeStart = (date: Date) => {
    // Keep the trip length when moving the start, so picking a start date doesn't invert the range.
    setEnd(new Date(date.getTime() + Math.max(0, days - 1) * DAY_MS));
    setStart(date);
  };

  const submit = () =>
    create.mutate(
      { name, startDate: toDateOnly(start), endDate: toDateOnly(end), groupId },
      {
        onSuccess: (trip) => {
          setName('');
          router.push(`/trips/${trip.id}`);
        },
      },
    );

  const mine = trips.data?.filter((t) => t.isOwner) ?? [];
  const sharedWithMe = trips.data?.filter((t) => !t.isOwner) ?? [];

  return (
    <Screen>
      <Section title="Plan a trip">
        <Card>
          <Input icon="map" placeholder="Trip name, e.g. Tour du Mont Blanc" value={name} onChangeText={setName} />
          <DateField label="Start" value={start} onChange={changeStart} minimumDate={today} />
          <DateField label="End" value={end} onChange={setEnd} minimumDate={start} />
          <ThemedText type="small" themeColor="textSecondary">
            {datesValid ? `${days} day${days > 1 ? 's' : ''}` : `A trip can be 1–${MAX_DAYS} days.`}
          </ThemedText>
          {!!groups.data?.length && (
            <View style={{ gap: Spacing.two }}>
              <ThemedText type="small" themeColor="textSecondary">
                Share with
              </ThemedText>
              <View style={styles.chips}>
                <Chip icon="lock" label="Just me" selected={groupId == null} onPress={() => setGroupId(null)} />
                {groups.data.map((g) => (
                  <Chip key={g.id} icon="person.3" label={g.name} selected={groupId === g.id} onPress={() => setGroupId(g.id)} />
                ))}
              </View>
            </View>
          )}
          {create.error && <ThemedText type="small">{create.error.message}</ThemedText>}
          <Button icon="plus" label="Create trip" onPress={submit} loading={create.isPending} disabled={!name.trim() || !datesValid} />
        </Card>
      </Section>

      <Section title="Your trips">
        <QueryState isLoading={trips.isLoading} error={trips.error} hasData={!!trips.data?.length} empty={trips.data?.length === 0} emptyText="No trips planned yet." emptyIcon="map" />
        {!!mine.length && <TripList trips={mine} />}
      </Section>

      {!!sharedWithMe.length && (
        <Section title="Shared with you">
          <TripList trips={sharedWithMe} />
        </Section>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
