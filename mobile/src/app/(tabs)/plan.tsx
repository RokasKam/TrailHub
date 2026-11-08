import { router } from 'expo-router';
import type { AppIcon } from '@/lib/icons';
import { Pressable, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { useRoutes } from '@/api/routes';
import { ThemedText } from '@/components/themed-text';
import { Card, Icon, List, ListRow, PageHeader, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import type { RouteSummary, Units } from '@/lib/types';

export default function PlanScreen() {
  const units = useUnits();
  const routes = useRoutes();
  const mine = routes.data?.filter((r) => r.isOwner) ?? [];
  const shared = routes.data?.filter((r) => !r.isOwner) ?? [];

  return (
    <Screen tabBar>
      <PageHeader eyebrow="Your own adventures" title="Plan" />

      <View style={styles.tiles}>
        <Tile
          icon="point.topleft.down.to.point.bottomright.curvepath.fill"
          color="#1F7A4D"
          title="Draw a route"
          text="Tap the map to place waypoints. TrailHub follows the hiking paths."
          onPress={() => router.push('/routes/new')}
        />
        <Tile
          icon="calendar.badge.clock"
          color="#7C3AED"
          title="Multi-day planner"
          text="Pick start, finish and days. Get stages with huts or campsites."
          onPress={() => router.push('/routes/plan')}
        />
      </View>

      <Section title="Your routes">
        <QueryState
          isLoading={routes.isLoading}
          error={routes.error}
          hasData={!!routes.data?.length}
          empty={mine.length === 0}
          emptyText="Routes you draw or plan show up here."
          emptyIcon="point.topleft.down.to.point.bottomright.curvepath"
          onRetry={() => routes.refetch()}
        />
        {mine.length > 0 && <RouteList routes={mine} units={units} />}
      </Section>

      {shared.length > 0 && (
        <Section title="Shared by your groups">
          <RouteList routes={shared} units={units} />
        </Section>
      )}
    </Screen>
  );
}

function RouteList({ routes, units }: { routes: RouteSummary[]; units: Units }) {
  return (
    <List>
      {routes.map((r) => (
        <ListRow
          key={r.id}
          icon={!r.isOwner ? 'person.3.fill' : r.dayCount > 1 ? 'calendar' : 'point.topleft.down.to.point.bottomright.curvepath'}
          iconColor={!r.isOwner ? '#2563EB' : r.dayCount > 1 ? '#7C3AED' : '#1F7A4D'}
          title={r.name}
          detail={[
            formatDistance(r.distanceMeters, units),
            `↑ ${formatElevation(r.ascentMeters, units)}`,
            r.dayCount > 1 ? `${r.dayCount} days` : formatMinutes(r.durationMinutes),
            r.published ? 'in Discover' : null,
          ]
            .filter(Boolean)
            .join(' · ')}
          onPress={() => router.push(`/routes/${r.id}`)}
        />
      ))}
    </List>
  );
}

function Tile({ icon, color, title, text, onPress }: { icon: AppIcon; color: string; title: string; text: string; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable onPress={onPress} accessibilityRole="button" style={({ pressed }) => [{ flex: 1 }, pressed && { opacity: 0.7 }]}>
      <Card style={styles.tile}>
        <View style={[styles.tileIcon, { backgroundColor: color }]}>
          <Icon name={icon} size={20} color="#fff" />
        </View>
        <ThemedText type="headline">{title}</ThemedText>
        <ThemedText type="small" themeColor="textSecondary">
          {text}
        </ThemedText>
        <View style={{ flex: 1 }} />
        <Icon name="arrow.right.circle.fill" size={22} color={theme.accent} />
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  tiles: { flexDirection: 'row', gap: Spacing.three },
  tile: { flex: 1, minHeight: 200 },
  tileIcon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center', marginBottom: Spacing.one },
});
