import { router, Stack, useLocalSearchParams } from 'expo-router';
import { Alert, View } from 'react-native';

import { downloadActivityGpx, useActivity, useDeleteActivity } from '@/api/activities';
import { useUnits } from '@/api/profile';
import { ElevationChart } from '@/components/charts';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Card, List, ListRow, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
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
      <Stack.Screen options={{ title: '' }} />
      <TrailMap height={300} lines={a.segments} bounds={a.bounds} />
      <View style={{ gap: 4 }}>
        <ThemedText type="subtitle">{a.name}</ThemedText>
        <ThemedText type="small" themeColor="textSecondary">
          {formatDate(a.startedAtUtc)} · {a.source === 'ImportedGpx' ? 'Imported from GPX' : 'Recorded'}
          {a.segments.length > 1 && ` · ${a.segments.length - 1} pause${a.segments.length > 2 ? 's' : ''}`}
        </ThemedText>
      </View>
      <Card>
        <StatRow>
          <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(a.distanceMeters, units)} />
          <Stat icon="timer" label="Moving" value={formatDuration(a.movingDurationSeconds)} />
          <Stat icon="clock" label="Total" value={formatDuration(a.elapsedDurationSeconds)} />
        </StatRow>
        <StatRow>
          <Stat icon="arrow.up.forward" label="Gain" value={formatElevation(a.elevationGainMeters, units)} />
          <Stat icon="arrow.down.forward" label="Loss" value={formatElevation(a.elevationLossMeters, units)} />
          <Stat icon="speedometer" label="Avg speed" value={formatPace(a.averageSpeedMetersPerSecond, units)} />
        </StatRow>
      </Card>
      {a.elevationProfile.length > 1 && (
        <Section title="Elevation">
          <Card>
            <ElevationChart samples={a.elevationProfile} units={units} />
          </Card>
        </Section>
      )}
      {a.notes && <ThemedText>{a.notes}</ThemedText>}
      <List>
        {a.trailId && <ListRow icon="map.fill" iconColor="#16A34A" title="View trail" onPress={() => router.push(`/trail/${a.trailId}`)} />}
        <ListRow
          icon="square.and.arrow.up"
          iconColor="#2563EB"
          title="Export GPX"
          onPress={async () => shareTextFile(`hike-${a.startedAtUtc.slice(0, 10)}.gpx`, await downloadActivityGpx(a.id)).catch((e: Error) => Alert.alert('Export failed', e.message))}
        />
      </List>
      <List>
        <ListRow title={remove.isPending ? 'Deleting…' : 'Delete hike'} destructive onPress={confirmDelete} />
      </List>
    </Screen>
  );
}
