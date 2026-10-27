import { router, Stack, useLocalSearchParams } from 'expo-router';
import { Alert } from 'react-native';

import { downloadActivityGpx, useActivity, useDeleteActivity } from '@/api/activities';
import { useUnits } from '@/api/profile';
import { ElevationChart } from '@/components/charts';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Button, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { formatDate, formatDistance, formatDuration, formatElevation, formatPace } from '@/lib/format';
import { shareTextFile } from '@/lib/share-file';

export default function ActivityScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const units = useUnits();
  const activity = useActivity(id);
  const remove = useDeleteActivity();

  if (!activity.data) {
    return (
      <Screen>
        <QueryState isLoading={activity.isLoading} error={activity.error} onRetry={() => activity.refetch()} />
      </Screen>
    );
  }
  const a = activity.data;

  const confirmDelete = () =>
    Alert.alert('Delete hike?', 'This removes it from your history and statistics.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Delete', style: 'destructive', onPress: () => remove.mutate(a.id, { onSuccess: () => router.back() }) },
    ]);

  return (
    <Screen>
      <Stack.Screen options={{ title: a.name }} />
      <TrailMap lines={a.segments} bounds={a.bounds} />
      <ThemedText type="small" themeColor="textSecondary">
        {formatDate(a.startedAtUtc)} · {a.source === 'ImportedGpx' ? 'Imported from GPX' : 'Recorded'}
        {a.segments.length > 1 && ` · ${a.segments.length - 1} pause${a.segments.length > 2 ? 's' : ''}`}
      </ThemedText>
      <StatRow>
        <Stat label="Distance" value={formatDistance(a.distanceMeters, units)} />
        <Stat label="Moving time" value={formatDuration(a.movingDurationSeconds)} />
        <Stat label="Total time" value={formatDuration(a.elapsedDurationSeconds)} />
      </StatRow>
      <StatRow>
        <Stat label="Elevation gain" value={formatElevation(a.elevationGainMeters, units)} />
        <Stat label="Elevation loss" value={formatElevation(a.elevationLossMeters, units)} />
        <Stat label="Avg speed" value={formatPace(a.averageSpeedMetersPerSecond, units)} />
      </StatRow>
      {a.elevationProfile.length > 1 && (
        <Section title="Elevation">
          <ElevationChart samples={a.elevationProfile} units={units} />
        </Section>
      )}
      {a.notes && <ThemedText type="small">{a.notes}</ThemedText>}
      {a.trailId && <Button label="View trail" variant="secondary" onPress={() => router.push(`/trail/${a.trailId}`)} />}
      <Button
        label="Export GPX"
        variant="secondary"
        onPress={async () => shareTextFile(`hike-${a.startedAtUtc.slice(0, 10)}.gpx`, await downloadActivityGpx(a.id)).catch((e: Error) => Alert.alert('Export failed', e.message))}
      />
      <Button label="Delete" variant="danger" onPress={confirmDelete} loading={remove.isPending} />
    </Screen>
  );
}
