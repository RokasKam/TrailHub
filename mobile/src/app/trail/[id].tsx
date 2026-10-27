import { router, Stack, useLocalSearchParams } from 'expo-router';
import type { SFSymbol } from 'expo-symbols';
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
import { Badge, Button, Card, Chip, Icon, Input, List, ListRow, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { WeatherCard } from '@/components/weather-card';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDate, formatDistance, formatElevation, formatMinutes } from '@/lib/format';
import { shareTextFile } from '@/lib/share-file';
import type { PointOfInterestType, TrailCondition } from '@/lib/types';
import { useRecordingStore } from '@/stores/recording-store';

const CONDITIONS: TrailCondition[] = ['Good', 'Muddy', 'Snow', 'Ice', 'Flooded', 'Overgrown', 'Blocked', 'Closed'];

export default function TrailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const units = useUnits();
  const theme = useTheme();
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
          title: '',
          headerRight: () => (
            <Pressable accessibilityLabel={t.isFavorite ? 'Remove from favorites' : 'Add to favorites'} onPress={() => favorite.mutate(!t.isFavorite)} hitSlop={12}>
              <Icon name={t.isFavorite ? 'heart.fill' : 'heart'} size={20} color={t.isFavorite ? '#E11D48' : theme.text} />
            </Pressable>
          ),
        }}
      />

      <TrailMap height={300} lines={route.data ? [route.data.points] : []} pois={t.pointsOfInterest} bounds={t.bounds} />

      <View style={styles.titleBlock}>
        <ThemedText type="subtitle">{t.name}</ThemedText>
        <View style={styles.subtitleRow}>
          <Icon name="mappin.and.ellipse" size={13} color={theme.textSecondary} />
          <ThemedText type="small" themeColor="textSecondary">
            {t.region}
          </ThemedText>
          {t.reviewCount > 0 && (
            <>
              <Icon name="star.fill" size={12} color="#F59E0B" />
              <ThemedText type="small" themeColor="textSecondary">
                {t.averageRating.toFixed(1)} ({t.reviewCount})
              </ThemedText>
            </>
          )}
        </View>
        <View style={styles.row}>
          <Badge label={t.difficulty} color={DIFFICULTY_COLORS[t.difficulty]} icon="figure.hiking" />
          {t.isLoop && <Badge label="Loop" color={theme.textSecondary} icon="arrow.triangle.2.circlepath" />}
        </View>
      </View>

      <Card>
        <StatRow>
          <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(t.distanceMeters, units)} />
          <Stat icon="arrow.up.forward" label="Gain" value={formatElevation(t.elevationGainMeters, units)} />
          <Stat icon="clock" label="Est. time" value={formatMinutes(t.estimatedDurationMinutes)} />
          {t.maxElevationMeters != null && <Stat icon="mountain.2" label="Highest" value={formatElevation(t.maxElevationMeters, units)} />}
        </StatRow>
      </Card>

      {t.latestCondition && (
        <Card style={[styles.condition, { backgroundColor: theme.accentSoft }]} flat>
          <Icon name={CONDITION_ICONS[t.latestCondition.condition]} size={20} color={theme.accent} />
          <View style={{ flex: 1 }}>
            <ThemedText type="smallBold">{t.latestCondition.condition} conditions</ThemedText>
            <ThemedText type="small" themeColor="textSecondary">
              Reported {formatDate(t.latestCondition.reportedAtUtc)}
              {t.latestCondition.note ? ` · ${t.latestCondition.note}` : ''}
            </ThemedText>
          </View>
        </Card>
      )}

      <View style={{ gap: Spacing.two + 2 }}>
        <Button
          size="large"
          icon={recordingStatus === 'idle' ? 'figure.hiking' : 'record.circle'}
          label={recordingStatus === 'idle' ? 'Start hike' : 'Recording in progress'}
          onPress={startHike}
        />
        <View style={styles.row}>
          <View style={{ flex: 1 }}>
            <Button label="Export GPX" icon="square.and.arrow.up" variant="tinted" onPress={exportGpx} />
          </View>
          <View style={{ flex: 1 }}>
            <Button
              label={offline == null ? 'Save offline' : offline >= 100 ? 'Saved offline' : `Saving ${offline}%`}
              icon={offline != null && offline >= 100 ? 'checkmark.circle.fill' : 'arrow.down.circle'}
              variant="tinted"
              onPress={saveOffline}
              disabled={offline != null && offline < 100}
            />
          </View>
        </View>
      </View>

      {elevation.data && (
        <Section title="Elevation">
          <Card>
            <ElevationChart samples={elevation.data} units={units} />
          </Card>
        </Section>
      )}

      <PhotoGallery trailId={t.id} photos={t.photos} />

      <Section title="Weather at the trailhead">
        <WeatherCard latitude={t.start.latitude} longitude={t.start.longitude} elevation={t.maxElevationMeters} />
      </Section>

      {t.description && (
        <Section title="About">
          <ThemedText>{t.description}</ThemedText>
        </Section>
      )}

      {t.pointsOfInterest.length > 0 && (
        <Section title="Points of interest">
          <List>
            {t.pointsOfInterest.map((p) => (
              <ListRow
                key={p.id}
                icon={POI_ICONS[p.type].icon}
                iconColor={POI_ICONS[p.type].color}
                title={p.name}
                detail={p.elevation != null ? formatElevation(p.elevation, units) : undefined}
              />
            ))}
          </List>
        </Section>
      )}

      <ReportCondition trailId={t.id} />
      <Reviews trailId={t.id} />
    </Screen>
  );
}

const POI_ICONS: Record<PointOfInterestType, { icon: SFSymbol; color: string }> = {
  WaterSource: { icon: 'drop.fill', color: '#2563EB' },
  Shelter: { icon: 'house.fill', color: '#92400E' },
  Campsite: { icon: 'tent.fill', color: '#15803D' },
  Viewpoint: { icon: 'binoculars.fill', color: '#9333EA' },
  Other: { icon: 'mappin', color: '#6B7280' },
};

const CONDITION_ICONS: Record<TrailCondition, SFSymbol> = {
  Good: 'checkmark.seal.fill',
  Muddy: 'drop.triangle.fill',
  Snow: 'snowflake',
  Ice: 'thermometer.snowflake',
  Flooded: 'water.waves',
  Overgrown: 'leaf.fill',
  Blocked: 'xmark.octagon.fill',
  Closed: 'nosign',
};

function ReportCondition({ trailId }: { trailId: string }) {
  const report = useReportCondition(trailId);
  return (
    <Section title="How is it out there?">
      <View style={styles.chips}>
        {CONDITIONS.map((c) => (
          <Chip
            key={c}
            icon={CONDITION_ICONS[c]}
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
        <ThemedText type="smallBold">Rate this trail</ThemedText>
        <View style={styles.row}>
          {[1, 2, 3, 4, 5].map((n) => (
            <Pressable key={n} onPress={() => setRating(n)} accessibilityLabel={`${n} stars`} hitSlop={6}>
              <Icon name={n <= rating ? 'star.fill' : 'star'} size={28} color={n <= rating ? '#F59E0B' : '#A8A29E'} weight="regular" />
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
          <View style={styles.row}>
            <Stars rating={r.rating} />
            <ThemedText type="caption" themeColor="textSecondary" style={{ marginLeft: 'auto' }}>
              {formatDate(r.createdAtUtc)}
            </ThemedText>
          </View>
          {r.comment && <ThemedText>{r.comment}</ThemedText>}
        </Card>
      ))}
    </Section>
  );
}

function Stars({ rating }: { rating: number }) {
  return (
    <View style={[styles.row, { gap: 2 }]}>
      {[1, 2, 3, 4, 5].map((n) => (
        <Icon key={n} name={n <= rating ? 'star.fill' : 'star'} size={13} color={n <= rating ? '#F59E0B' : '#A8A29E'} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  titleBlock: { gap: Spacing.one + 2 },
  subtitleRow: { flexDirection: 'row', alignItems: 'center', gap: 5, flexWrap: 'wrap' },
  condition: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  row: { flexDirection: 'row', gap: Spacing.two, alignItems: 'center' },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
