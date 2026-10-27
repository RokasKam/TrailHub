import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import {
  downloadTrailGpx,
  useElevationProfile,
  useReportCondition,
  useReviews,
  useSubmitReview,
  useToggleFavorite,
  useTrail,
  useTrailRoute,
} from '@/api/trails';
import { ElevationChart } from '@/components/charts';
import { PhotoGallery } from '@/components/photo-gallery';
import { ThemedText } from '@/components/themed-text';
import { DIFFICULTY_COLORS } from '@/components/trail-card';
import { downloadOfflineRegion, offlineRegionProgress, TrailMap, useMapStyleUrl } from '@/components/trail-map';
import { Button, Card, Chip, Input, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { WeatherCard } from '@/components/weather-card';
import { Spacing } from '@/constants/theme';
import { formatDate, formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import { shareTextFile } from '@/lib/share-file';
import type { TrailCondition } from '@/lib/types';
import { useRecordingStore } from '@/stores/recording-store';

const CONDITIONS: TrailCondition[] = ['Good', 'Muddy', 'Snow', 'Ice', 'Flooded', 'Overgrown', 'Blocked', 'Closed'];

export default function TrailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const units = useUnits();
  const trail = useTrail(id);
  const route = useTrailRoute(id);
  const elevation = useElevationProfile(id);
  const favorite = useToggleFavorite(id);
  const styleUrl = useMapStyleUrl();
  const startRecording = useRecordingStore((s) => s.start);
  const recordingStatus = useRecordingStore((s) => s.status);
  const [offline, setOffline] = useState<number | null>(null);
  useEffect(() => {
    // Show "Saved offline" for regions downloaded earlier, not just in this visit.
    void offlineRegionProgress(`trail-${id}`).then((progress) => progress != null && setOffline(progress));
  }, [id]);

  if (!trail.data) {
    return (
      <Screen>
        <QueryState isLoading={trail.isLoading} error={trail.error} onRetry={() => trail.refetch()} />
      </Screen>
    );
  }
  const t = trail.data;

  const startHike = () => {
    if (recordingStatus !== 'idle') return Alert.alert('Already recording', 'Finish your current hike first.');
    startRecording({ id: t.id, name: t.name });
    router.navigate('/record');
  };

  const exportGpx = async () => {
    try {
      await shareTextFile(`${t.name.replace(/[^\w-]+/g, '-').toLowerCase()}.gpx`, await downloadTrailGpx(t.id));
    } catch (e) {
      Alert.alert('Export failed', (e as Error).message);
    }
  };

  const saveOffline = async () => {
    setOffline(0);
    // Warm the query cache too, so details + full route are available without signal.
    await Promise.all([route.refetch(), elevation.refetch()]);
    try {
      await downloadOfflineRegion(`trail-${t.id}`, t.bounds, styleUrl, setOffline);
    } catch (e) {
      setOffline(null);
      Alert.alert('Offline download failed', (e as Error).message);
    }
  };

  return (
    <Screen>
      <Stack.Screen
        options={{
          title: t.name,
          headerRight: () => (
            <Pressable accessibilityLabel={t.isFavorite ? 'Remove from favorites' : 'Add to favorites'} onPress={() => favorite.mutate(!t.isFavorite)} hitSlop={12}>
              <ThemedText style={{ fontSize: 22 }}>{t.isFavorite ? '★' : '☆'}</ThemedText>
            </Pressable>
          ),
        }}
      />

      <TrailMap lines={route.data ? [route.data.points] : []} pois={t.pointsOfInterest} bounds={t.bounds} />

      <View style={styles.headerRow}>
        <View style={[styles.badge, { backgroundColor: DIFFICULTY_COLORS[t.difficulty] }]}>
          <ThemedText type="small" style={styles.badgeText}>
            {t.difficulty}
          </ThemedText>
        </View>
        <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
          {t.region}
          {t.reviewCount > 0 && ` · ★ ${t.averageRating.toFixed(1)} (${t.reviewCount})`}
        </ThemedText>
      </View>

      <StatRow>
        <Stat label="Distance" value={formatDistance(t.distanceMeters, units)} />
        <Stat label="Elevation gain" value={formatElevation(t.elevationGainMeters, units)} />
        <Stat label="Est. time" value={formatMinutes(t.estimatedDurationMinutes)} />
        {t.maxElevationMeters != null && <Stat label="Highest point" value={formatElevation(t.maxElevationMeters, units)} />}
      </StatRow>

      {t.latestCondition && (
        <Card>
          <ThemedText type="smallBold">
            Conditions: {t.latestCondition.condition} · {formatDate(t.latestCondition.reportedAtUtc)}
          </ThemedText>
          {t.latestCondition.note && <ThemedText type="small">{t.latestCondition.note}</ThemedText>}
        </Card>
      )}

      <Button label={recordingStatus === 'idle' ? 'Start hike on this trail' : 'Recording in progress'} onPress={startHike} />
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button label="Export GPX" variant="secondary" onPress={exportGpx} />
        </View>
        <View style={{ flex: 1 }}>
          <Button
            label={offline == null ? 'Save offline' : offline >= 100 ? 'Saved offline ✓' : `Saving… ${offline}%`}
            variant="secondary"
            onPress={saveOffline}
            disabled={offline != null && offline < 100}
          />
        </View>
      </View>

      {elevation.data && (
        <Section title="Elevation">
          <ElevationChart samples={elevation.data} units={units} />
        </Section>
      )}

      <PhotoGallery trailId={t.id} photos={t.photos} />

      <Section title="Weather at the trailhead">
        <WeatherCard latitude={t.start.latitude} longitude={t.start.longitude} elevation={t.maxElevationMeters} />
      </Section>

      {t.description && (
        <Section title="About">
          <ThemedText type="small">{t.description}</ThemedText>
        </Section>
      )}

      {t.pointsOfInterest.length > 0 && (
        <Section title="Points of interest">
          {t.pointsOfInterest.map((p) => (
            <ThemedText key={p.id} type="small">
              {POI_ICONS[p.type]} {p.name}
              {p.elevation != null && ` · ${formatElevation(p.elevation, units)}`}
            </ThemedText>
          ))}
        </Section>
      )}

      <ReportCondition trailId={t.id} />
      <Reviews trailId={t.id} />
    </Screen>
  );
}

const POI_ICONS = { WaterSource: '💧', Shelter: '🛖', Campsite: '⛺️', Viewpoint: '🔭', Other: '📍' } as const;

function ReportCondition({ trailId }: { trailId: string }) {
  const report = useReportCondition(trailId);
  return (
    <Section title="Report conditions">
      <View style={styles.chips}>
        {CONDITIONS.map((c) => (
          <Chip
            key={c}
            label={c}
            onPress={() => report.mutate({ condition: c }, { onSuccess: () => Alert.alert('Thanks!', 'Your report helps other hikers.') })}
          />
        ))}
      </View>
    </Section>
  );
}

function Reviews({ trailId }: { trailId: string }) {
  const reviews = useReviews(trailId);
  const submit = useSubmitReview(trailId);
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');

  return (
    <Section title="Reviews">
      <Card>
        <View style={styles.row}>
          {[1, 2, 3, 4, 5].map((n) => (
            <Pressable key={n} onPress={() => setRating(n)} accessibilityLabel={`${n} stars`} hitSlop={6}>
              <ThemedText style={{ fontSize: 28 }}>{n <= rating ? '★' : '☆'}</ThemedText>
            </Pressable>
          ))}
        </View>
        <Input placeholder="How was it? (optional)" value={comment} onChangeText={setComment} multiline />
        <Button
          label="Post review"
          disabled={rating === 0}
          loading={submit.isPending}
          onPress={() => submit.mutate({ rating, comment: comment || undefined }, { onSuccess: () => setComment('') })}
        />
      </Card>
      {reviews.data?.items.map((r) => (
        <Card key={r.id}>
          <ThemedText type="smallBold">
            {'★'.repeat(r.rating)}
            {'☆'.repeat(5 - r.rating)} · {formatDate(r.createdAtUtc)}
          </ThemedText>
          {r.comment && <ThemedText type="small">{r.comment}</ThemedText>}
        </Card>
      ))}
    </Section>
  );
}

const styles = StyleSheet.create({
  headerRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  badge: { borderRadius: Spacing.two, paddingHorizontal: Spacing.two },
  badgeText: { color: '#fff', fontSize: 12 },
  row: { flexDirection: 'row', gap: Spacing.two, alignItems: 'center' },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
