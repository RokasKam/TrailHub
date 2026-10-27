import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Location from 'expo-location';
import * as TaskManager from 'expo-task-manager';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { api, ApiError } from '@/lib/api';

export const GROUP_SHARING_TASK = 'trailhub-group-location-sharing';

type GroupSharingState = {
  /** Groups the user is sharing their live location with. Persisted so sharing survives app restarts. */
  groupIds: string[];
  add: (groupId: string) => void;
  remove: (groupId: string) => void;
};

export const useGroupSharingStore = create<GroupSharingState>()(
  persist(
    (set) => ({
      groupIds: [],
      add: (groupId) => set((s) => ({ groupIds: s.groupIds.includes(groupId) ? s.groupIds : [...s.groupIds, groupId] })),
      remove: (groupId) => set((s) => ({ groupIds: s.groupIds.filter((id) => id !== groupId) })),
    }),
    { name: 'group-location-sharing', storage: createJSONStorage(() => AsyncStorage) },
  ),
);

async function sendLocation(groupId: string, coords: Location.LocationObjectCoords) {
  try {
    await api(`/api/groups/${groupId}/locations/mine`, {
      method: 'POST',
      body: {
        latitude: coords.latitude,
        longitude: coords.longitude,
        elevation: coords.altitude ?? null,
        accuracyMeters: coords.accuracy ?? null,
        headingDegrees: coords.heading != null && coords.heading >= 0 ? coords.heading : null,
      },
    });
  } catch (error) {
    // Left or removed from the group: stop sharing with it. Network errors just skip this update.
    if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
      useGroupSharingStore.getState().remove(groupId);
    }
  }
}

// Defined at module scope (imported from the root layout) so the OS can deliver locations in the
// background. REST rather than SignalR: a WebSocket doesn't survive the app being suspended.
TaskManager.defineTask<{ locations: Location.LocationObject[] }>(GROUP_SHARING_TASK, async ({ data, error }) => {
  if (error || !data?.locations.length) return;
  // The OS may launch the app in the background just to run this task, before storage is loaded.
  if (!useGroupSharingStore.persist.hasHydrated()) await useGroupSharingStore.persist.rehydrate();

  const latest = data.locations[data.locations.length - 1];
  const { groupIds } = useGroupSharingStore.getState();
  if (groupIds.length === 0) {
    await Location.stopLocationUpdatesAsync(GROUP_SHARING_TASK).catch(() => undefined);
    return;
  }
  await Promise.all(groupIds.map((id) => sendLocation(id, latest.coords)));
});

/** Starts sharing with a group (also while the app is in the background). Returns false if not permitted. */
export async function startGroupSharing(groupId: string): Promise<{ ok: boolean; background: boolean }> {
  const foreground = await Location.requestForegroundPermissionsAsync();
  if (foreground.status !== 'granted') return { ok: false, background: false };
  const background = await Location.requestBackgroundPermissionsAsync().catch(() => ({ status: 'denied' }));

  useGroupSharingStore.getState().add(groupId);

  // Send a first position right away so the group sees you without waiting for movement.
  const first = await Location.getLastKnownPositionAsync().catch(() => null);
  if (first) await sendLocation(groupId, first.coords);

  if (!(await Location.hasStartedLocationUpdatesAsync(GROUP_SHARING_TASK).catch(() => false))) {
    await Location.startLocationUpdatesAsync(GROUP_SHARING_TASK, {
      // Balanced accuracy and 25 m steps: plenty for "where is everyone", much kinder to the battery than recording.
      accuracy: Location.Accuracy.Balanced,
      distanceInterval: 25,
      timeInterval: 30_000,
      pausesUpdatesAutomatically: false,
      showsBackgroundLocationIndicator: true,
      foregroundService: {
        notificationTitle: 'Sharing your location',
        notificationBody: 'Your group can see where you are on the map.',
        killServiceOnDestroy: false,
      },
    });
  }
  return { ok: true, background: background.status === 'granted' };
}

export async function stopGroupSharing(groupId: string) {
  useGroupSharingStore.getState().remove(groupId);
  await api(`/api/groups/${groupId}/locations/mine`, { method: 'DELETE' }).catch(() => undefined);
  if (useGroupSharingStore.getState().groupIds.length === 0) {
    await Location.stopLocationUpdatesAsync(GROUP_SHARING_TASK).catch(() => undefined);
  }
}
