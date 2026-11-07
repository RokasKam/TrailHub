import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Crypto from 'expo-crypto';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import type { TrackPoint } from '@/lib/types';
import { useSyncStore } from '@/stores/sync-store';

type RecordingStatus = 'idle' | 'recording' | 'paused';

type RecordingState = {
  status: RecordingStatus;
  /** Generated on the device when recording starts; the server uses it as the activity id (idempotent upload). */
  activityId: string | null;
  trailId: string | null;
  /** The planned route being followed, if the hike was started from one. */
  routeId: string | null;
  trailName: string | null;
  /** The line to follow, drawn dashed on the map. Stored with the recording so it works offline. */
  guide: { latitude: number; longitude: number }[] | null;
  startedAtUtc: string | null;
  /** One array per stretch between pause/resume. */
  segments: TrackPoint[][];
  /** Starts recording, optionally following a Discover trail or (a day of) a planned route. */
  start: (follow?: { trail?: { id: string; name: string }; route?: { id: string; name: string; guide: { latitude: number; longitude: number }[] } }) => void;
  pause: () => void;
  resume: () => void;
  addPoints: (points: TrackPoint[]) => void;
  /** Hands the finished recording to the sync queue and resets. Returns the activity id. */
  finish: (name: string, notes?: string) => string | null;
  discard: () => void;
};

const initial = { status: 'idle' as const, activityId: null, trailId: null, routeId: null, trailName: null, guide: null, startedAtUtc: null, segments: [] };

/**
 * The active recording. Persisted on every change so a crash or the OS killing the app mid-hike
 * doesn't lose the track; the background location task appends to it via getState().
 */
export const useRecordingStore = create<RecordingState>()(
  persist(
    (set, get) => ({
      ...initial,
      start: (follow) =>
        set({
          status: 'recording',
          activityId: Crypto.randomUUID(),
          trailId: follow?.trail?.id ?? null,
          routeId: follow?.route?.id ?? null,
          trailName: follow?.trail?.name ?? follow?.route?.name ?? null,
          guide: follow?.route?.guide ?? null,
          startedAtUtc: new Date().toISOString(),
          segments: [[]],
        }),
      pause: () => get().status === 'recording' && set({ status: 'paused' }),
      resume: () => get().status === 'paused' && set((s) => ({ status: 'recording', segments: [...s.segments, []] })),
      addPoints: (points) => {
        if (get().status !== 'recording' || points.length === 0) return;
        set((s) => {
          const segments = s.segments.length ? [...s.segments] : [[]];
          const current = segments[segments.length - 1];
          const lastTime = current.length ? Date.parse(current[current.length - 1].timestampUtc) : 0;
          // Location batches can arrive out of order or duplicated after the app wakes up.
          const fresh = points.filter((p) => Date.parse(p.timestampUtc) > lastTime).sort((a, b) => a.timestampUtc.localeCompare(b.timestampUtc));
          segments[segments.length - 1] = [...current, ...fresh];
          return { segments };
        });
      },
      finish: (name, notes) => {
        const { activityId, trailId, routeId, segments } = get();
        const nonEmpty = segments.filter((s) => s.length > 0);
        if (!activityId || nonEmpty.reduce((n, s) => n + s.length, 0) < 2) {
          set(initial);
          return null;
        }
        useSyncStore.getState().enqueue({ id: activityId, name, trailId, routeId, notes: notes ?? null, segments: nonEmpty });
        set(initial);
        return activityId;
      },
      discard: () => set(initial),
    }),
    { name: 'active-recording', storage: createJSONStorage(() => AsyncStorage) },
  ),
);
