import { router } from 'expo-router';
import type { AppIcon } from '@/lib/icons';
import { Alert, StyleSheet, View } from 'react-native';

import { useActivities, useImportActivityGpx, useStatistics } from '@/api/activities';
import { useUnits } from '@/api/profile';
import { BarChart } from '@/components/charts';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Icon, IconButton, List, ListRow, PageHeader, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDate, formatDistance, formatDuration, formatElevation } from '@/lib/format';
import { pickGpxFile } from '@/lib/pick-gpx';
import { useSyncStore } from '@/stores/sync-store';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export default function ActivitiesScreen() {
  const units = useUnits();
  const theme = useTheme();
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

  const year = stats.data?.thisYear;

  return (
    <Screen tabBar>
      <PageHeader
        eyebrow={year ? `${year.hikes} hike${year.hikes === 1 ? '' : 's'} this year` : undefined}
        title="Hikes"
        action={<IconButton icon="square.and.arrow.down" label="Import GPX" onPress={pickGpx} />}
      />

      {stats.data && year && (
        <>
          <Card style={[styles.hero, { backgroundColor: theme.accent }]}>
            <ThemedText type="caption" style={styles.heroLabel}>
              {new Date().getFullYear()} DISTANCE
            </ThemedText>
            <ThemedText type="number" style={styles.heroNumber}>
              {formatDistance(year.distanceMeters, units)}
            </ThemedText>
            <View style={styles.heroStats}>
              <HeroStat icon="figure.hiking" value={String(year.hikes)} label={year.hikes === 1 ? 'hike' : 'hikes'} />
              <HeroStat icon="arrow.up.forward" value={formatElevation(year.elevationGainMeters, units)} label="climbed" />
              <HeroStat icon="clock" value={formatDuration(year.movingDurationSeconds)} label="moving" />
            </View>
          </Card>

          <Section title="Last 12 months">
            <Card>
              <BarChart
                values={months.map((m) => m.distanceMeters)}
                labels={months.map((m) => `${MONTHS[m.month - 1]} ’${String(m.year).slice(2)}`)}
              />
            </Card>
          </Section>

          <Section title="All time">
            <Card>
              <StatRow>
                <Stat icon="figure.hiking" label="Hikes" value={String(stats.data.allTime.hikes)} />
                <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(stats.data.allTime.distanceMeters, units)} />
                <Stat icon="arrow.up.forward" label="Climbed" value={formatElevation(stats.data.allTime.elevationGainMeters, units)} />
                <Stat icon="clock" label="Time" value={formatDuration(stats.data.allTime.movingDurationSeconds)} />
              </StatRow>
            </Card>
          </Section>
        </>
      )}

      <Section title="History">
        {pendingCount > 0 && (
          <View style={styles.pending}>
            <Icon name="icloud.and.arrow.up" size={14} color={theme.textSecondary} />
            <ThemedText type="small" themeColor="textSecondary">
              {pendingCount} hike{pendingCount > 1 ? 's' : ''} waiting to upload (see Record)
            </ThemedText>
          </View>
        )}
        <QueryState
          isLoading={activities.isLoading}
          error={activities.error}
          hasData={items.length > 0}
          empty={!activities.isLoading && items.length === 0}
          emptyText="No hikes yet. Record one or import a GPX file."
          emptyIcon="figure.hiking"
          onRetry={() => activities.refetch()}
        />
        {items.length > 0 && (
          <List>
            {items.map((a) => (
              <ListRow
                key={a.id}
                icon={a.source === 'ImportedGpx' ? 'square.and.arrow.down.fill' : 'figure.hiking'}
                iconColor={a.source === 'ImportedGpx' ? '#64748B' : theme.accent}
                title={a.name}
                detail={`${formatDate(a.startedAtUtc)} · ${formatDistance(a.distanceMeters, units)} · ↑ ${formatElevation(a.elevationGainMeters, units)} · ${formatDuration(a.movingDurationSeconds)}`}
                onPress={() => router.push(`/activity/${a.id}`)}
              />
            ))}
          </List>
        )}
        {activities.hasNextPage && <Button label="Load more" variant="secondary" onPress={() => activities.fetchNextPage()} loading={activities.isFetchingNextPage} />}
      </Section>
    </Screen>
  );
}

function HeroStat({ icon, value, label }: { icon: AppIcon; value: string; label: string }) {
  return (
    <View style={styles.heroStat}>
      <Icon name={icon} size={13} color="rgba(255,255,255,0.75)" />
      <ThemedText type="smallBold" style={{ color: '#fff' }}>
        {value}
      </ThemedText>
      <ThemedText type="small" style={{ color: 'rgba(255,255,255,0.75)' }}>
        {label}
      </ThemedText>
    </View>
  );
}

const styles = StyleSheet.create({
  hero: { gap: Spacing.one, padding: Spacing.four - 4 },
  heroLabel: { color: 'rgba(255,255,255,0.75)', letterSpacing: 0.8 },
  heroNumber: { color: '#fff', fontSize: 48, lineHeight: 56, fontWeight: 800, letterSpacing: -1 },
  heroStats: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.three, marginTop: Spacing.one },
  heroStat: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  pending: { flexDirection: 'row', alignItems: 'center', gap: 6 },
});
