import AsyncStorage from '@react-native-async-storage/async-storage';
import { onlineManager } from '@tanstack/react-query';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { api, ApiError, describeError } from '@/lib/api';
import { queryClient } from '@/lib/query-client';
import type { ActivitySummary, TrackPoint } from '@/lib/types';

export type PendingActivity = {
  id: string;
  name: string;
  trailId: string | null;
  notes: string | null;
  segments: TrackPoint[][];
  attempts?: number;
  lastError?: string | null;
  /** Set when the server rejected the upload (4xx); retrying won't help, so it waits for the user. */
  rejected?: boolean;
};

type SyncState = {
  pending: PendingActivity[];
  syncing: boolean;
  enqueue: (activity: PendingActivity) => void;
  remove: (id: string) => void;
  flush: () => Promise<void>;
};

/**
 * Outbox for finished hikes. Uploads use PUT /api/activities/{id} with the id generated on the
 * device, so a retry after a timeout (upload succeeded, response lost) can never create a duplicate.
 */
export const useSyncStore = create<SyncState>()(
  persist(
    (set, get) => ({
      pending: [],
      syncing: false,
      enqueue: (activity) => {
        set((s) => ({ pending: [...s.pending.filter((p) => p.id !== activity.id), { ...activity, attempts: 0, lastError: null }] }));
        void get().flush();
      },
      remove: (id) => set((s) => ({ pending: s.pending.filter((p) => p.id !== id) })),
      flush: async () => {
        if (get().syncing || !onlineManager.isOnline()) return;
        set({ syncing: true });
        try {
          for (const item of get().pending.filter((p) => !p.rejected)) {
            try {
              await api<ActivitySummary>(`/api/activities/${item.id}`, {
                method: 'PUT',
                body: { name: item.name, trailId: item.trailId, notes: item.notes, segments: item.segments },
              });
              get().remove(item.id);
            } catch (error) {
              const rejected = error instanceof ApiError && error.isClientError && error.status !== 401;
              set((s) => ({
                pending: s.pending.map((p) =>
                  p.id === item.id ? { ...p, attempts: (p.attempts ?? 0) + 1, lastError: describeError(error as Error), rejected } : p,
                ),
              }));
              if (!rejected) break; // network/server trouble: try again later, keep order
            }
          }
        } finally {
          set({ syncing: false });
          await queryClient.invalidateQueries({ queryKey: ['activities'] });
          await queryClient.invalidateQueries({ queryKey: ['profile'] });
        }
      },
    }),
    {
      name: 'activity-outbox',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: (s) => ({ pending: s.pending }),
    },
  ),
);

// Flush whenever connectivity comes back.
onlineManager.subscribe((online) => {
  if (online) void useSyncStore.getState().flush();
});
