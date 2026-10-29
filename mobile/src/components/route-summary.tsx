import { StyleSheet, View } from 'react-native';

import { ElevationChart } from '@/components/charts';
import { ThemedText } from '@/components/themed-text';
import { Card, Icon, Section, Stat, StatRow } from '@/components/ui';
import { poiColor } from '@/components/trail-map';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import type { PlannedRoute, PoiKind, RouteDay, Units } from '@/lib/types';

export const POI_LABELS: Record<PoiKind, string> = {
  WaterSource: 'Water',
  Campsite: 'Campsite',
  MountainHut: 'Mountain hut',
  Shelter: 'Shelter',
  Hotel: 'Hotel',
  Viewpoint: 'Viewpoint',
};

/** Distance, climbing, time (or days) and the trail grade of a planned route. */
export function RouteStats({ route, units }: { route: PlannedRoute; units: Units }) {
  const s = route.stats;
  return (
    <Card>
      <StatRow>
        <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(s.distanceMeters, units)} />
        <Stat icon="arrow.up.forward" label="Ascent" value={formatElevation(s.ascentMeters, units)} />
        <Stat icon="arrow.down.forward" label="Descent" value={formatElevation(s.descentMeters, units)} />
        <Stat icon="clock" label={route.days.length > 1 ? `${route.days.length} days` : 'Walking'} value={formatMinutes(s.durationMinutes)} />
      </StatRow>
      <ThemedText type="small" themeColor="textSecondary">
        {s.maxElevationMeters != null && `Highest point ${formatElevation(s.maxElevationMeters, units)}`}
        {s.maxSacScale > 0 && ` · hardest section T${s.maxSacScale} (${SAC_LABELS[s.maxSacScale]})`}
        {' · time excludes breaks'}
      </ThemedText>
    </Card>
  );
}

const SAC_LABELS: Record<number, string> = { 1: 'hiking', 2: 'mountain hiking', 3: 'demanding', 4: 'alpine', 5: 'demanding alpine', 6: 'difficult alpine' };

/** Elevation profile with dashed lines where each day starts. */
export function RouteElevation({ route, units }: { route: PlannedRoute; units: Units }) {
  if (route.elevationProfile.length < 2) return null;
  return (
    <Section title="Elevation">
      <Card>
        <ElevationChart
          samples={route.elevationProfile}
          units={units}
          markers={route.days.length > 1 ? route.days.map((d) => ({ distanceMeters: d.startDistanceMeters, label: `D${d.day}` })) : []}
        />
      </Card>
    </Section>
  );
}

/** One row per day: distance, gain, time and where the night is. */
export function Itinerary({ days, units }: { days: RouteDay[]; units: Units }) {
  const theme = useTheme();
  if (days.length < 2) return null;
  return (
    <Section title="Itinerary">
      <Card style={{ gap: 0, paddingVertical: Spacing.two }}>
        <View style={[styles.row, styles.header, { borderBottomColor: theme.separator }]}>
          <ThemedText type="caption" themeColor="textSecondary" style={styles.day}>
            DAY
          </ThemedText>
          <ThemedText type="caption" themeColor="textSecondary" style={styles.num}>
            DISTANCE
          </ThemedText>
          <ThemedText type="caption" themeColor="textSecondary" style={styles.num}>
            GAIN
          </ThemedText>
          <ThemedText type="caption" themeColor="textSecondary" style={styles.night}>
            OVERNIGHT
          </ThemedText>
        </View>
        {days.map((d, i) => (
          <View key={d.day} style={[styles.row, i < days.length - 1 && { borderBottomColor: theme.separator, borderBottomWidth: StyleSheet.hairlineWidth }]}>
            <ThemedText type="number" style={[styles.day, { fontSize: 17 }]}>
              {d.day}
            </ThemedText>
            <View style={styles.num}>
              <ThemedText type="smallBold">{formatDistance(d.stats.distanceMeters, units)}</ThemedText>
              <ThemedText type="caption" themeColor="textSecondary">
                {formatMinutes(d.stats.durationMinutes)}
              </ThemedText>
            </View>
            <ThemedText type="smallBold" style={styles.num}>
              {formatElevation(d.stats.ascentMeters, units)}
            </ThemedText>
            <View style={[styles.night, styles.inline]}>
              {d.overnight ? (
                <>
                  <Icon
                    name={d.overnight.kind === 'Campsite' ? 'tent.fill' : d.overnight.kind === 'Hotel' ? 'bed.double.fill' : d.overnight.kind ? 'house.lodge.fill' : 'moon.zzz.fill'}
                    size={13}
                    color={d.overnight.kind ? poiColor(d.overnight.kind) : '#9CA3AF'}
                  />
                  <ThemedText type="small" numberOfLines={2} style={{ flex: 1 }}>
                    {d.overnight.name ?? 'Overnight stop'}
                  </ThemedText>
                </>
              ) : (
                <>
                  <Icon name="flag.checkered" size={13} color={theme.accent} />
                  <ThemedText type="small" themeColor="textSecondary">
                    Finish
                  </ThemedText>
                </>
              )}
            </View>
          </View>
        ))}
      </Card>
    </Section>
  );
}

export function RouteWarnings({ warnings }: { warnings: string[] }) {
  if (warnings.length === 0) return null;
  return (
    <Card flat style={{ backgroundColor: '#FEF3C7' }}>
      {warnings.map((w) => (
        <View key={w} style={styles.inline}>
          <Icon name="exclamationmark.triangle.fill" size={13} color="#B45309" />
          <ThemedText type="small" style={{ flex: 1, color: '#78350F' }}>
            {w}
          </ThemedText>
        </View>
      ))}
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two, paddingVertical: Spacing.two + 2 },
  header: { borderBottomWidth: StyleSheet.hairlineWidth, paddingTop: 0 },
  day: { width: 30 },
  num: { width: 76 },
  night: { flex: 1 },
  inline: { flexDirection: 'row', alignItems: 'center', gap: 6 },
});
