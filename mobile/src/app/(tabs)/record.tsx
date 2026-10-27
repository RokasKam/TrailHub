import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { useTrailRoute } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Button, Card, Input, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDistance, formatDuration, formatElevation, formatPace } from '@/lib/format';
import { trackMetrics } from '@/lib/geo';
import { requestTrackingPermissions, startTracking, stopTracking } from '@/location/tracking';
import { useRecordingStore } from '@/stores/recording-store';
import { useSyncStore } from '@/stores/sync-store';

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
  const { status, segments, trailId, trailName, startedAtUtc, start, pause, resume, finish, discard } = useRecordingStore();
  const guide = useTrailRoute(status === 'idle' ? null : trailId).data?.points; // cached with the trail, so it works offline
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
      <ThemedText type="subtitle">{status === 'idle' ? 'Record' : (trailName ?? 'Recording')}</ThemedText>

      {status === 'idle' ? (
        <>
          <ThemedText themeColor="textSecondary">
            Recording works without signal. Your hike is saved on the phone and uploads automatically when you&apos;re back online.
          </ThemedText>
          <Button label="Start recording" onPress={begin} />
          <ThemedText type="small" themeColor="textSecondary">
            Tip: open a trail and tap “Start hike on this trail” to link the hike to it.
          </ThemedText>
        </>
      ) : (
        <>
          <TrailMap lines={segments} guide={guide} showUser followUser height={300} center={last} zoom={15} />
          <StatRow>
            <Stat label="Distance" value={formatDistance(metrics.distanceMeters, units)} />
            <Stat label="Moving time" value={formatDuration(metrics.movingSeconds)} />
            <Stat label="Elevation gain" value={formatElevation(metrics.elevationGainMeters, units)} />
            <Stat label="Avg speed" value={formatPace(metrics.movingSeconds > 0 ? metrics.distanceMeters / metrics.movingSeconds : 0, units)} />
          </StatRow>
          <ThemedText type="small" themeColor="textSecondary">
            {status === 'paused' ? 'Paused' : 'Recording'} · {pointCount} GPS points
            {last?.elevation != null && ` · altitude ${formatElevation(last.elevation, units)}`}
          </ThemedText>

          {finishing ? (
            <Card>
              <Input placeholder={trailName ?? 'Name this hike'} value={name} onChangeText={setName} autoFocus returnKeyType="done" onSubmitEditing={save} />
              <View style={styles.row}>
                <View style={{ flex: 1 }}>
                  <Button label="Back" variant="secondary" onPress={() => setFinishing(false)} />
                </View>
                <View style={{ flex: 1 }}>
                  <Button label="Save hike" onPress={save} />
                </View>
              </View>
            </Card>
          ) : (
            <View style={styles.row}>
              <View style={{ flex: 1 }}>
                <Button label={status === 'recording' ? 'Pause' : 'Resume'} variant="secondary" onPress={togglePause} />
              </View>
              <View style={{ flex: 1 }}>
                <Button label="Finish" onPress={() => setFinishing(true)} />
              </View>
            </View>
          )}
          <Button label="Discard" variant="danger" onPress={confirmDiscard} />
        </>
      )}

      <PendingUploads />
    </Screen>
  );
}

function PendingUploads() {
  const { pending, syncing, flush, remove } = useSyncStore();
  if (pending.length === 0) return null;
  return (
    <Section title="Waiting to upload">
      {pending.map((p) => (
        <Card key={p.id}>
          <ThemedText type="smallBold">{p.name}</ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            {p.segments.reduce((n, s) => n + s.length, 0)} points
            {p.rejected ? ` · rejected: ${p.lastError}` : p.lastError ? ' · upload failed, will retry' : ' · uploads when online'}
          </ThemedText>
          {p.rejected && <Button label="Remove" variant="danger" onPress={() => remove(p.id)} />}
        </Card>
      ))}
      <Button label={syncing ? 'Uploading…' : 'Upload now'} variant="secondary" onPress={() => void flush()} loading={syncing} />
    </Section>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: Spacing.two },
});
