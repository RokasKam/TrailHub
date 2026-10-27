import { Link, router } from 'expo-router';
import { useState } from 'react';
import { Pressable } from 'react-native';

import { useCreateTrip, useTrips } from '@/api/planning';
import { DateField, toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Input, QueryState, Screen, Section } from '@/components/ui';
import { formatDate } from '@/lib/format';

const DAY_MS = 24 * 60 * 60 * 1000;
const MAX_DAYS = 30;

export default function TripsScreen() {
  const trips = useTrips();
  const create = useCreateTrip();
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
      { name, startDate: toDateOnly(start), endDate: toDateOnly(end) },
      {
        onSuccess: (trip) => {
          setName('');
          router.push(`/trips/${trip.id}`);
        },
      },
    );

  return (
    <Screen>
      <Section title="Plan a trip">
        <Card>
          <Input placeholder="Trip name, e.g. Tour du Mont Blanc" value={name} onChangeText={setName} />
          <DateField label="Start" value={start} onChange={changeStart} minimumDate={today} />
          <DateField label="End" value={end} onChange={setEnd} minimumDate={start} />
          <ThemedText type="small" themeColor="textSecondary">
            {datesValid ? `${days} day${days > 1 ? 's' : ''}` : `A trip can be 1–${MAX_DAYS} days.`}
          </ThemedText>
          {create.error && <ThemedText type="small">{create.error.message}</ThemedText>}
          <Button label="Create trip" onPress={submit} loading={create.isPending} disabled={!name.trim() || !datesValid} />
        </Card>
      </Section>

      <Section title="Your trips">
        <QueryState isLoading={trips.isLoading} error={trips.error} hasData={!!trips.data?.length} empty={trips.data?.length === 0} emptyText="No trips planned yet." />
        {trips.data?.map((t) => (
          <Link key={t.id} href={`/trips/${t.id}`} asChild>
            <Pressable>
              <Card>
                <ThemedText type="smallBold">{t.name}</ThemedText>
                <ThemedText type="small" themeColor="textSecondary">
                  {formatDate(t.startDate)} – {formatDate(t.endDate)} · {t.dayCount} day{t.dayCount > 1 ? 's' : ''} · {t.trailCount} trail
                  {t.trailCount === 1 ? '' : 's'} · {t.status}
                </ThemedText>
              </Card>
            </Pressable>
          </Link>
        ))}
      </Section>
    </Screen>
  );
}
