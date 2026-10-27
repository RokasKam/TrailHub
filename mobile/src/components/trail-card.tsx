import { Link } from 'expo-router';
import { Pressable, StyleSheet, View } from 'react-native';

import { ThemedText } from '@/components/themed-text';
import { Card } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import type { Difficulty, TrailSummary, Units } from '@/lib/types';

export const DIFFICULTY_COLORS: Record<Difficulty, string> = {
  Easy: '#15803D',
  Moderate: '#CA8A04',
  Hard: '#C2410C',
  Expert: '#7F1D1D',
};

export function TrailCard({ trail, units }: { trail: TrailSummary; units: Units }) {
  return (
    <Link href={`/trail/${trail.id}`} asChild>
      <Pressable style={({ pressed }) => pressed && { opacity: 0.8 }}>
        <Card>
          <View style={styles.header}>
            <View style={{ flex: 1 }}>
              <ThemedText type="smallBold" numberOfLines={1} style={styles.name}>
                {trail.name}
              </ThemedText>
              <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
                {trail.region}
                {trail.distanceFromSearchPointMeters != null && ` · ${formatDistance(trail.distanceFromSearchPointMeters, units)} away`}
              </ThemedText>
            </View>
            <View style={[styles.badge, { backgroundColor: DIFFICULTY_COLORS[trail.difficulty] }]}>
              <ThemedText type="small" style={styles.badgeText}>
                {trail.difficulty}
              </ThemedText>
            </View>
          </View>
          <ThemedText type="small">
            {formatDistance(trail.distanceMeters, units)} · ↑ {formatElevation(trail.elevationGainMeters, units)} · ~{formatMinutes(trail.estimatedDurationMinutes)}
            {trail.isLoop ? ' · Loop' : ''}
            {trail.reviewCount > 0 ? ` · ★ ${trail.averageRating.toFixed(1)} (${trail.reviewCount})` : ''}
          </ThemedText>
        </Card>
      </Pressable>
    </Link>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', gap: Spacing.two, alignItems: 'flex-start' },
  name: { fontSize: 16 },
  badge: { borderRadius: Spacing.two, paddingHorizontal: Spacing.two, paddingVertical: Spacing.half },
  badgeText: { color: '#fff', fontSize: 12, lineHeight: 18 },
});
