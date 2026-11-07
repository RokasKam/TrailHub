import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';

import { routeGuide } from '@/api/routes';
import { ThemedText } from '@/components/themed-text';
import { SaveRouteOffline } from '@/components/offline-route';
import { Button, Card, Chip } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDistance, formatMinutes } from '@/lib/format';
import type { PlannedRoute, Units } from '@/lib/types';
import { useRecordingStore } from '@/stores/recording-store';

/** Starts recording a hike that follows a route (or one day of it), then opens the Record tab. */
export function useStartRouteHike() {
  const status = useRecordingStore((s) => s.status);
  const start = useRecordingStore((s) => s.start);
  return (route: PlannedRoute, day?: number) => {
    if (!route.id) return;
    if (status !== 'idle') return Alert.alert('Already recording', 'Finish your current hike first.');
    const multiDay = route.days.length > 1 && day;
    start({
      route: {
        id: route.id,
        name: multiDay ? `${route.name ?? 'Route'} · day ${day}` : (route.name ?? 'Route'),
        guide: routeGuide(route, multiDay ? day : undefined),
      },
    });
    router.navigate('/record');
  };
}

/** "Hike it": pick a day of a multi-day route (or the whole route) and start recording along it. */
export function HikeRoute({ route, units }: { route: PlannedRoute; units: Units }) {
  const startHike = useStartRouteHike();
  const [day, setDay] = useState(1);
  const multiDay = route.days.length > 1;
  const stats = multiDay ? route.days[day - 1]?.stats : route.stats;

  return (
    <Card>
      {multiDay && (
        <View style={styles.chips}>
          {route.days.map((d) => (
            <Chip key={d.day} label={`Day ${d.day}`} selected={day === d.day} onPress={() => setDay(d.day)} />
          ))}
        </View>
      )}
      {stats && (
        <ThemedText type="small" themeColor="textSecondary">
          {formatDistance(stats.distanceMeters, units)} · {formatMinutes(stats.durationMinutes)} walking. The route is shown as a dashed line while you record, and works offline.
        </ThemedText>
      )}
      <Button size="large" icon="figure.hiking" label={multiDay ? `Start day ${day}` : 'Start hike'} onPress={() => startHike(route, multiDay ? day : undefined)} />
      {route.id && <SaveRouteOffline route={route} />}
    </Card>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
