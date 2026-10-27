import { Image } from 'expo-image';
import { Link } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';

import { ThemedText } from '@/components/themed-text';
import { Badge, Card, Glass, Icon } from '@/components/ui';
import { config } from '@/config';
import { Radius, Spacing } from '@/constants/theme';
import { useColorScheme } from '@/hooks/use-color-scheme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import type { Difficulty, LatLng, TrailSummary, Units } from '@/lib/types';

export const DIFFICULTY_COLORS: Record<Difficulty, string> = {
  Easy: '#16A34A',
  Moderate: '#D97706',
  Hard: '#EA580C',
  Expert: '#B91C1C',
};

/** Static Mapbox image of the trailhead, cached by expo-image, so cards have a picture without loading a live map each. */
export function trailheadImageUrl(start: LatLng, width = 600, height = 300, dark = false, zoom = 11.5) {
  const at = `${start.longitude.toFixed(5)},${start.latitude.toFixed(5)}`;
  const style = dark ? 'dark-v11' : 'outdoors-v12';
  return `https://api.mapbox.com/styles/v1/mapbox/${style}/static/pin-s+1f7a4d(${at})/${at},${zoom},0/${width}x${height}@2x?access_token=${config.mapboxToken}`;
}

export function TrailCard({ trail, units, compact }: { trail: TrailSummary; units: Units; compact?: boolean }) {
  const theme = useTheme();
  const dark = useColorScheme() === 'dark';
  return (
    <Link href={`/trail/${trail.id}`} asChild>
      <Pressable style={({ pressed }) => [compact && styles.compact, { transform: [{ scale: pressed ? 0.98 : 1 }] }]}>
        <Card style={styles.card}>
          <View>
            <Image
              source={{ uri: trailheadImageUrl(trail.start, compact ? 520 : 700, compact ? 300 : 320, dark) }}
              style={[styles.image, compact && styles.imageCompact, { backgroundColor: theme.backgroundSelected }]}
              contentFit="cover"
              transition={200}
              cachePolicy="disk"
            />
            <Glass style={styles.difficulty}>
              <View style={[styles.dot, { backgroundColor: DIFFICULTY_COLORS[trail.difficulty] }]} />
              <ThemedText type="caption" style={{ fontWeight: 700 }}>
                {trail.difficulty}
              </ThemedText>
            </Glass>
            {trail.reviewCount > 0 && (
              <Glass style={styles.rating}>
                <Icon name="star.fill" size={11} color="#F59E0B" />
                <ThemedText type="caption" style={{ fontWeight: 700 }}>
                  {trail.averageRating.toFixed(1)}
                </ThemedText>
              </Glass>
            )}
          </View>
          <View style={styles.body}>
            <ThemedText type="headline" numberOfLines={1}>
              {trail.name}
            </ThemedText>
            <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
              {trail.region}
              {trail.distanceFromSearchPointMeters != null && ` · ${formatDistance(trail.distanceFromSearchPointMeters, units)} away`}
            </ThemedText>
            <View style={styles.meta}>
              <Meta icon="point.topleft.down.to.point.bottomright.curvepath" text={formatDistance(trail.distanceMeters, units)} />
              <Meta icon="arrow.up.forward" text={formatElevation(trail.elevationGainMeters, units)} />
              <Meta icon="clock" text={formatMinutes(trail.estimatedDurationMinutes)} />
              {trail.isLoop && !compact && <Badge label="Loop" color={theme.textSecondary} icon="arrow.triangle.2.circlepath" />}
            </View>
          </View>
        </Card>
      </Pressable>
    </Link>
  );
}

function Meta({ icon, text }: { icon: Parameters<typeof Icon>[0]['name']; text: string }) {
  const theme = useTheme();
  return (
    <View style={styles.metaItem}>
      <Icon name={icon} size={12} color={theme.textSecondary} />
      <ThemedText type="small" style={{ fontWeight: 600 }}>
        {text}
      </ThemedText>
    </View>
  );
}

const styles = StyleSheet.create({
  compact: { width: 260 },
  card: { padding: 0, overflow: 'hidden', gap: 0 },
  image: { width: '100%', height: 150 },
  imageCompact: { height: 120 },
  difficulty: { position: 'absolute', top: Spacing.two + 2, left: Spacing.two + 2, flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 10, paddingVertical: 5, borderRadius: Radius.pill, overflow: 'hidden' },
  rating: { position: 'absolute', top: Spacing.two + 2, right: Spacing.two + 2, flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: 10, paddingVertical: 5, borderRadius: Radius.pill, overflow: 'hidden' },
  dot: { width: 8, height: 8, borderRadius: 4 },
  body: { padding: Spacing.three, gap: 2 },
  meta: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: Spacing.three, marginTop: Spacing.two },
  metaItem: { flexDirection: 'row', alignItems: 'center', gap: 5 },
});
