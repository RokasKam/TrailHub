import { router } from 'expo-router';
import type { SFSymbol } from 'expo-symbols';
import { Pressable, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { useRoutes } from '@/api/routes';
import { ThemedText } from '@/components/themed-text';
import { Card, Icon, List, ListRow, PageHeader, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';

export default function PlanScreen() {
  const units = useUnits();
  const routes = useRoutes();

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
          empty={routes.data?.length === 0}
          emptyText="Routes you draw or plan show up here."
          emptyIcon="point.topleft.down.to.point.bottomright.curvepath"
          onRetry={() => routes.refetch()}
        />
        {!!routes.data?.length && (
          <List>
            {routes.data.map((r) => (
              <ListRow
                key={r.id}
                icon={r.dayCount > 1 ? 'calendar' : 'point.topleft.down.to.point.bottomright.curvepath'}
                iconColor={r.dayCount > 1 ? '#7C3AED' : '#1F7A4D'}
                title={r.name}
                detail={[
                  formatDistance(r.distanceMeters, units),
                  `↑ ${formatElevation(r.ascentMeters, units)}`,
                  r.dayCount > 1 ? `${r.dayCount} days` : formatMinutes(r.durationMinutes),
                ].join(' · ')}
                onPress={() => router.push(`/routes/${r.id}`)}
              />
            ))}
          </List>
        )}
      </Section>
    </Screen>
  );
}

function Tile({ icon, color, title, text, onPress }: { icon: SFSymbol; color: string; title: string; text: string; onPress: () => void }) {
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
