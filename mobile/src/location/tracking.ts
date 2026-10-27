import * as Location from 'expo-location';
import * as TaskManager from 'expo-task-manager';

import type { TrackPoint } from '@/lib/types';
import { useRecordingStore } from '@/stores/recording-store';

export const LOCATION_TASK = 'trailhub-location-tracking';

/** Fixes worse than this are usually indoor/urban-canyon noise and make the track zig-zag. */
const MAX_ACCURACY_METERS = 40;

// Must be defined at module scope (imported from the root layout) so the OS can wake the JS
// runtime and deliver locations while the app is in the background or the screen is locked.
TaskManager.defineTask<{ locations: Location.LocationObject[] }>(LOCATION_TASK, async ({ data, error }) => {
  if (error || !data) return;
  // If the OS relaunched the app in the background just to deliver locations, the persisted
  // recording may not be loaded yet; without this the store reads "idle" and points are dropped.
  if (!useRecordingStore.persist.hasHydrated()) await useRecordingStore.persist.rehydrate();
  const points: TrackPoint[] = data.locations
    .filter((l) => (l.coords.accuracy ?? 0) <= MAX_ACCURACY_METERS)
    .map((l) => ({
      latitude: l.coords.latitude,
      longitude: l.coords.longitude,
      elevation: l.coords.altitude ?? null,
      timestampUtc: new Date(l.timestamp).toISOString(),
    }));
  useRecordingStore.getState().addPoints(points);
});

export async function requestTrackingPermissions() {
  const foreground = await Location.requestForegroundPermissionsAsync();
  if (foreground.status !== 'granted') return { granted: false, background: false };
  // Background permission is what lets recording continue with the screen off. If the user
  // declines, recording still works while the app is open.
  const background = await Location.requestBackgroundPermissionsAsync().catch(() => ({ status: 'denied' }));
  return { granted: true, background: background.status === 'granted' };
}

export async function startTracking() {
  if (await Location.hasStartedLocationUpdatesAsync(LOCATION_TASK).catch(() => false)) return;
  await Location.startLocationUpdatesAsync(LOCATION_TASK, {
    accuracy: Location.Accuracy.BestForNavigation,
    activityType: Location.ActivityType.Fitness,
    distanceInterval: 5,
    timeInterval: 5000,
    deferredUpdatesInterval: 5000,
    pausesUpdatesAutomatically: false,
    showsBackgroundLocationIndicator: true,
    foregroundService: {
      notificationTitle: 'Recording your hike',
      notificationBody: 'TrailHub is tracking your route.',
      killServiceOnDestroy: false,
    },
  });
}

export async function stopTracking() {
  if (await Location.hasStartedLocationUpdatesAsync(LOCATION_TASK).catch(() => false)) {
    await Location.stopLocationUpdatesAsync(LOCATION_TASK);
  }
}
