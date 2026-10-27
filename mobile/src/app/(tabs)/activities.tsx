import { Link } from 'expo-router';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useActivities, useImportActivityGpx, useStatistics } from '@/api/activities';
import { useUnits } from '@/api/profile';
import { BarChart } from '@/components/charts';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDate, formatDistance, formatDuration, formatElevation } from '@/lib/format';
import { pickGpxFile } from '@/lib/pick-gpx';
import type { ActivityTotals, Units } from '@/lib/types';
import { useSyncStore } from '@/stores/sync-store';

const MONTHS = ['J', 'F', 'M', 'A', 'M', 'J', 'J', 'A', 'S', 'O', 'N', 'D'];

export default function ActivitiesScreen() {
  const units = useUnits();
  const stats = useStatistics();
  const activities = useActivities();
  const importGpx = useImportActivityGpx();
  const pendingCount = useSyncStore((s) => s.pending.length);

  const pickGpx = async () => {
    const file = await pickGpxFile();
    if (!file) return;
    importGpx.mutate({ uri: file.uri, fileName: file.name }, { onError: (e) => Alert.alert('Import failed', e.message) });
  };

  const items = activities.data?.pages.flatMap((p) => p.items) ?? [];
  const months = stats.data?.lastTwelveMonths ?? [];

  return (
    <Screen tabBar>
      <View style={styles.titleRow}>
        <ThemedText type="subtitle">Hikes</ThemedText>
        <Button label="Import GPX" variant="secondary" onPress={pickGpx} loading={importGpx.isPending} />
      </View>

      {stats.data && (
        <>
          <Totals title="All time" totals={stats.data.allTime} units={units} />
          <Totals title={`${new Date().getFullYear()}`} totals={stats.data.thisYear} units={units} />
          <Section title="Distance per month">
            <Card>
              <BarChart
                values={months.map((m) => m.distanceMeters)}
                labels={months.map((m) => `${MONTHS[m.month - 1]} ${String(m.year).slice(2)}`)}
              />
            </Card>
          </Section>
        </>
      )}

      <Section title="History">
        {pendingCount > 0 && (
          <ThemedText type="small" themeColor="textSecondary">
            {pendingCount} hike{pendingCount > 1 ? 's' : ''} waiting to upload (see Record).
          </ThemedText>
        )}
        <QueryState
          isLoading={activities.isLoading}
          error={activities.error}
          hasData={items.length > 0}
          empty={!activities.isLoading && items.length === 0}
          emptyText="No hikes yet. Record one or import a GPX file."
          onRetry={() => activities.refetch()}
        />
        {items.map((a) => (
          <Link key={a.id} href={`/activity/${a.id}`} asChild>
            <Pressable style={({ pressed }) => pressed && { opacity: 0.8 }}>
              <Card>
                <ThemedText type="smallBold">{a.name}</ThemedText>
                <ThemedText type="small" themeColor="textSecondary">
                  {formatDate(a.startedAtUtc)} · {formatDistance(a.distanceMeters, units)} · ↑ {formatElevation(a.elevationGainMeters, units)} ·{' '}
                  {formatDuration(a.movingDurationSeconds)}
                </ThemedText>
              </Card>
            </Pressable>
          </Link>
        ))}
        {activities.hasNextPage && <Button label="Load more" variant="secondary" onPress={() => activities.fetchNextPage()} loading={activities.isFetchingNextPage} />}
      </Section>
    </Screen>
  );
}

function Totals({ title, totals, units }: { title: string; totals: ActivityTotals; units: Units }) {
  return (
    <Section title={title}>
      <Card>
        <StatRow>
          <Stat label="Hikes" value={String(totals.hikes)} />
          <Stat label="Distance" value={formatDistance(totals.distanceMeters, units)} />
          <Stat label="Elevation" value={formatElevation(totals.elevationGainMeters, units)} />
          <Stat label="Time" value={formatDuration(totals.movingDurationSeconds)} />
        </StatRow>
      </Card>
    </Section>
  );
}

const styles = StyleSheet.create({
  titleRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: Spacing.two },
});
