import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import type { AppIcon } from '@/lib/icons';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { useTrailRoute } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Button, Card, Icon, Input, List, ListRow, PageHeader, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatDuration, formatElevation, formatPace } from '@/lib/format';
import { trackMetrics } from '@/lib/geo';
import { requestTrackingPermissions, startTracking, stopTracking } from '@/location/tracking';
import { useRecordingStore } from '@/stores/recording-store';
import { useSyncStore, waitForUpload } from '@/stores/sync-store';

function useNow(active: boolean) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, [active]);
  return now;
}

/** The recording itself is kept even if GPS can't start; points arrive once location works again. */
async function startTrackingOrWarn() {
  try {
    await startTracking();
  } catch (e) {
    Alert.alert('GPS unavailable', `Location updates couldn't start: ${(e as Error).message}`);
  }
}

export default function RecordScreen() {
  const units = useUnits();
  const theme = useTheme();
  const { status, segments, trailId, trailName, guide: routeGuide, startedAtUtc, start, pause, resume, finish, discard } = useRecordingStore();
  const trailGuide = useTrailRoute(status === 'idle' ? null : trailId).data?.points; // cached with the trail, so it works offline
  const guide = routeGuide ?? trailGuide;
  const now = useNow(status === 'recording');
  const [name, setName] = useState('');
  const [finishing, setFinishing] = useState(false);

  // If the app was killed mid-hike, the persisted recording comes back: resume GPS updates.
  useEffect(() => {
    if (status === 'recording') void startTracking().catch(() => undefined);
  }, [status]);

  const begin = async () => {
    const permission = await requestTrackingPermissions();
    if (!permission.granted) return Alert.alert('Location needed', 'TrailHub needs your location to record a hike.');
    if (!permission.background) {
      Alert.alert('Recording only while open', 'Allow "Always" location access in Settings to keep recording with the screen off.');
    }
    if (status === 'idle') start();
    await startTrackingOrWarn();
  };

  const togglePause = async () => {
    if (status === 'recording') {
      pause();
      await stopTracking(); // saves battery during breaks
    } else {
      resume();
      await startTrackingOrWarn();
    }
  };

  const save = async () => {
    await stopTracking();
    const fallback = trailName ?? `Hike on ${new Date(startedAtUtc ?? Date.now()).toLocaleDateString()}`;
    const id = finish(name.trim() || fallback);
    setFinishing(false);
    setName('');
    if (!id) return Alert.alert('Nothing recorded', 'No GPS points were recorded for this hike.');
    router.navigate('/activities');
    // Sharing needs the hike on the server: offer it once the upload is through, otherwise point to later.
    if (await waitForUpload(id)) {
      Alert.alert('Share with your group?', 'Let your groups see this hike, with photos and a caption. You choose how much of the map they see.', [
        { text: 'Not now', style: 'cancel' },
        { text: 'Share', onPress: () => router.push(`/activity/share?id=${id}`) },
      ]);
    } else {
      Alert.alert('Hike saved', 'It will upload when you’re back online. You can share it with your groups later from the hike’s page.');
    }
  };

  const confirmDiscard = () =>
    Alert.alert('Discard hike?', 'The recorded track will be deleted.', [
      { text: 'Keep', style: 'cancel' },
      {
        text: 'Discard',
        style: 'destructive',
        onPress: async () => {
          await stopTracking();
          discard();
          setFinishing(false);
        },
      },
    ]);

  const metrics = trackMetrics(segments, status === 'paused' ? undefined : now);
  const pointCount = segments.reduce((n, s) => n + s.length, 0);
  const last = segments.at(-1)?.at(-1);

  return (
    <Screen tabBar>
      <PageHeader
        eyebrow={status === 'idle' ? 'Ready when you are' : status === 'paused' ? 'Paused' : 'Recording'}
        title={status === 'idle' ? 'Record' : (trailName ?? 'Your hike')}
        action={status !== 'idle' && <LiveDot paused={status === 'paused'} />}
      />

      {status === 'idle' ? (
        <Card style={styles.idle}>
          <Pressable accessibilityRole="button" accessibilityLabel="Start recording" onPress={begin} style={({ pressed }) => ({ transform: [{ scale: pressed ? 0.95 : 1 }] })}>
            <View style={[styles.startRing, { borderColor: theme.accentSoft }]}>
              <View style={[styles.start, { backgroundColor: theme.accent }]}>
                <Icon name="figure.hiking" size={44} color="#fff" />
                <ThemedText type="headline" style={{ color: '#fff' }}>
                  Start
                </ThemedText>
              </View>
            </View>
          </Pressable>
          <ThemedText themeColor="textSecondary" style={styles.center}>
            Works without signal. Your hike is saved on the phone and uploads when you&apos;re back online.
          </ThemedText>
          <View style={styles.tip}>
            <Icon name="lightbulb.fill" size={14} color="#D97706" />
            <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
              Open a trail and tap Start hike to follow its route.
            </ThemedText>
          </View>
        </Card>
      ) : (
        <>
          <View style={styles.timer}>
            <ThemedText type="number" style={styles.timerText}>
              {formatDuration(metrics.movingSeconds)}
            </ThemedText>
            <ThemedText type="caption" themeColor="textSecondary">
              MOVING TIME · {pointCount} GPS POINTS
            </ThemedText>
          </View>

          {/* Until the first GPS fix, show the whole route to follow instead of the globe. */}
          <TrailMap
            lines={segments}
            guide={guide}
            showUser
            followUser={!!last || !guide?.length}
            bounds={!last && guide?.length ? guideBounds(guide) : undefined}
            height={280}
            center={last}
            zoom={15}
          />

          <Card>
            <StatRow>
              <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(metrics.distanceMeters, units)} />
              <Stat icon="arrow.up.forward" label="Gain" value={formatElevation(metrics.elevationGainMeters, units)} />
              <Stat icon="speedometer" label="Avg speed" value={formatPace(metrics.movingSeconds > 0 ? metrics.distanceMeters / metrics.movingSeconds : 0, units)} />
              {last?.elevation != null && <Stat icon="mountain.2" label="Altitude" value={formatElevation(last.elevation, units)} />}
            </StatRow>
          </Card>

          {finishing ? (
            <Card>
              <ThemedText type="headline">Name your hike</ThemedText>
              <Input icon="pencil" placeholder={trailName ?? 'Morning ridge walk'} value={name} onChangeText={setName} autoFocus returnKeyType="done" onSubmitEditing={save} />
              <View style={styles.row}>
                <View style={{ flex: 1 }}>
                  <Button label="Back" variant="secondary" onPress={() => setFinishing(false)} />
                </View>
                <View style={{ flex: 1 }}>
                  <Button label="Save hike" icon="checkmark" onPress={save} />
                </View>
              </View>
            </Card>
          ) : (
            <View style={styles.controls}>
              <RoundButton icon="trash" label="Discard" size={56} background={theme.dangerSoft} color={theme.danger} onPress={confirmDiscard} />
              <RoundButton
                icon={status === 'recording' ? 'pause.fill' : 'play.fill'}
                label={status === 'recording' ? 'Pause' : 'Resume'}
                size={84}
                background={status === 'recording' ? theme.text : theme.accent}
                color={status === 'recording' ? theme.background : '#fff'}
                onPress={togglePause}
              />
              <RoundButton icon="flag.checkered" label="Finish" size={56} background={theme.accentSoft} color={theme.accent} onPress={() => setFinishing(true)} />
            </View>
          )}
        </>
      )}

      <PendingUploads />
    </Screen>
  );
}

function RoundButton({ icon, label, size, background, color, onPress }: { icon: AppIcon; label: string; size: number; background: string; color: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress} style={({ pressed }) => [styles.roundButton, { transform: [{ scale: pressed ? 0.93 : 1 }] }]}>
      <View style={{ width: size, height: size, borderRadius: size / 2, backgroundColor: background, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={size * 0.36} color={color} />
      </View>
      <ThemedText type="caption" themeColor="textSecondary">
        {label}
      </ThemedText>
    </Pressable>
  );
}

function LiveDot({ paused }: { paused: boolean }) {
  const theme = useTheme();
  return (
    <View style={[styles.live, { backgroundColor: paused ? theme.backgroundSelected : theme.dangerSoft }]}>
      <View style={[styles.liveDot, { backgroundColor: paused ? theme.textSecondary : '#EF4444' }]} />
      <ThemedText type="caption" style={{ fontWeight: 700, color: paused ? theme.textSecondary : '#EF4444' }}>
        {paused ? 'PAUSED' : 'LIVE'}
      </ThemedText>
    </View>
  );
}

function PendingUploads() {
  const theme = useTheme();
  const { pending, syncing, flush, remove } = useSyncStore();
  if (pending.length === 0) return null;
  return (
    <Section title="Waiting to upload" action={<Button size="small" variant="tinted" icon="arrow.up.circle" label={syncing ? 'Uploading…' : 'Upload now'} onPress={() => void flush()} loading={syncing} />}>
      <List>
        {pending.map((p) => (
          <ListRow
            key={p.id}
            icon={p.rejected ? 'exclamationmark.triangle.fill' : 'icloud.and.arrow.up'}
            iconColor={p.rejected ? theme.danger : '#64748B'}
            title={p.name}
            detail={`${p.segments.reduce((n, s) => n + s.length, 0)} points · ${p.rejected ? `rejected: ${p.lastError}` : p.lastError ? 'upload failed, will retry' : 'uploads when online'}`}
            trailing={p.rejected ? <Button size="small" variant="danger" label="Remove" onPress={() => remove(p.id)} /> : undefined}
          />
        ))}
      </List>
    </Section>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: Spacing.two },
  center: { textAlign: 'center' },
  idle: { alignItems: 'center', gap: Spacing.four, paddingVertical: Spacing.five },
  startRing: { padding: 10, borderRadius: 999, borderWidth: 10 },
  start: { width: 150, height: 150, borderRadius: 75, alignItems: 'center', justifyContent: 'center', gap: 4 },
  tip: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  timer: { alignItems: 'center', gap: 2 },
  timerText: { fontSize: 64, lineHeight: 72, fontWeight: 800, letterSpacing: -1 },
  controls: { flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-evenly' },
  roundButton: { alignItems: 'center', gap: 6 },
  live: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 10, paddingVertical: 5, borderRadius: 999, marginBottom: 6 },
  liveDot: { width: 8, height: 8, borderRadius: 4 },
});

function guideBounds(points: { latitude: number; longitude: number }[]) {
  const lats = points.map((p) => p.latitude);
  const lons = points.map((p) => p.longitude);
  return { minLatitude: Math.min(...lats), minLongitude: Math.min(...lons), maxLatitude: Math.max(...lats), maxLongitude: Math.max(...lons) };
}
