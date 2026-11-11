import { router } from 'expo-router';
import { type ReactNode, useState } from 'react';
import { ActivityIndicator, Alert, ScrollView, StyleSheet, View } from 'react-native';

import { useActivities, useImportActivityGpx, useShareTargets, useStatistics } from '@/api/activities';
import { useFeed } from '@/api/feed';
import { useUnits } from '@/api/profile';
import { BarChart } from '@/components/charts';
import { FeedPostCard } from '@/components/feed-post-card';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Chip, EmptyState, Icon, IconButton, List, ListRow, PageHeader, QueryState, Screen, Section, Segmented, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { ApiError } from '@/lib/api';
import { formatDate, formatDistance, formatDuration, formatElevation } from '@/lib/format';
import type { AppIcon } from '@/lib/icons';
import { pickGpxFile } from '@/lib/pick-gpx';
import { useSyncStore } from '@/stores/sync-store';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

type HikesView = 'mine' | 'feed';

export default function ActivitiesScreen() {
  const [view, setView] = useState<HikesView>('mine');
  const toggle = (
    <Segmented<HikesView>
      options={[
        { value: 'mine', label: 'My hikes', icon: 'figure.hiking' },
        { value: 'feed', label: 'Feed', icon: 'person.3.fill' },
      ]}
      value={view}
      onChange={setView}
    />
  );
  return view === 'mine' ? <MyHikes toggle={toggle} /> : <Feed toggle={toggle} />;
}

function MyHikes({ toggle }: { toggle: ReactNode }) {
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
    <Screen
      tabBar
      refreshing={stats.isRefetching || (activities.isRefetching && !activities.isFetchingNextPage)}
      onRefresh={() => {
        void stats.refetch();
        void activities.refetch();
      }}
      onEndReached={() => activities.hasNextPage && !activities.isFetchingNextPage && void activities.fetchNextPage()}>
      <PageHeader
        eyebrow={year ? `${year.hikes} hike${year.hikes === 1 ? '' : 's'} this year` : undefined}
        title="Hikes"
        action={<IconButton icon="square.and.arrow.down" label="Import GPX" onPress={pickGpx} />}
      />
      {toggle}

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

/** Hikes your groups shared (yours included), newest first. */
function Feed({ toggle }: { toggle: ReactNode }) {
  const [groupId, setGroupId] = useState<string | null>(null);
  const targets = useShareTargets();
  const groups = targets.data ?? [];
  // A stale filter (you left that group) 404s; fall back to all groups.
  const activeGroup = groups.some((g) => g.groupId === groupId) ? groupId : null;
  const feed = useFeed(activeGroup);
  const posts = feed.data?.pages.flatMap((p) => p.items) ?? [];
  const notMember = feed.error instanceof ApiError && feed.error.status === 404 && activeGroup !== null;
  const noGroups = targets.isSuccess && groups.length === 0;

  return (
    <Screen
      tabBar
      refreshing={feed.isRefetching && !feed.isFetchingNextPage}
      onRefresh={() => {
        void targets.refetch();
        void feed.refetch();
      }}
      onEndReached={() => feed.hasNextPage && !feed.isFetchingNextPage && void feed.fetchNextPage()}>
      <PageHeader eyebrow="Your groups" title="Hikes" />
      {toggle}

      {groups.length > 1 && (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips} style={styles.chipScroll}>
          <Chip label="All groups" selected={activeGroup === null} onPress={() => setGroupId(null)} />
          {groups.map((g) => (
            <Chip key={g.groupId} label={g.name} selected={activeGroup === g.groupId} onPress={() => setGroupId(g.groupId)} />
          ))}
        </ScrollView>
      )}

      {notMember ? (
        <Card style={{ alignItems: 'center' }}>
          <EmptyState icon="person.3" title="You’re not in this group anymore" />
          <Button label="Show all groups" variant="tinted" size="small" onPress={() => setGroupId(null)} />
        </Card>
      ) : posts.length === 0 && !feed.isLoading && !feed.error ? (
        <Card style={{ alignItems: 'center' }}>
          {noGroups ? (
            <>
              <EmptyState icon="person.3" title="No groups yet" message="Join or start a group, then share a hike with it to see it here." />
              <Button label="Go to Groups" variant="tinted" icon="person.3" size="small" onPress={() => router.push('/groups')} />
            </>
          ) : (
            <EmptyState
              icon="figure.hiking"
              title="Share a hike with your group to see it here"
              message="Open any hike in My hikes and tap Share. Hikes your group members share show up here too."
            />
          )}
        </Card>
      ) : (
        <QueryState isLoading={feed.isLoading} error={feed.error} hasData={posts.length > 0} onRetry={() => feed.refetch()} />
      )}

      {posts.map((p) => (
        <FeedPostCard key={p.activityId} post={p} />
      ))}
      {feed.isFetchingNextPage && <ActivityIndicator style={{ marginVertical: Spacing.three }} />}
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
  chipScroll: { marginHorizontal: -(Spacing.three + 2) },
  chips: { gap: Spacing.two, paddingHorizontal: Spacing.three + 2 },
});
