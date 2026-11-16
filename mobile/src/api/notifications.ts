import { type InfiniteData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AppNotification, NotificationPreferences, Paged, PushPlatform } from '@/lib/types';

// The inbox changes all the time and is useless offline, so none of it is persisted to disk.
export const notificationKeys = {
  all: ['notifications'] as const,
  list: ['notifications', 'list'] as const,
  unreadCount: ['notifications', 'unread-count'] as const,
  preferences: ['notifications', 'preferences'] as const,
};

/** Inbox, most recently updated first (a batched like moves back to the top when someone else likes). */
export function useNotifications() {
  return useInfiniteQuery({
    queryKey: notificationKeys.list,
    queryFn: ({ pageParam, signal }) => api<Paged<AppNotification>>('/api/notifications', { query: { page: pageParam, pageSize: 30 }, signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    meta: { persist: false },
  });
}

/**
 * Unread badge count. Refetches when the app comes to the foreground (focusManager follows AppState), every
 * 60 s while the app is active, and when a push arrives (see NotificationsBridge).
 */
export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount,
    queryFn: ({ signal }) => api<{ count: number }>('/api/notifications/unread-count', { signal }),
    select: (data) => data.count,
    staleTime: 15 * 1000,
    refetchInterval: 60 * 1000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: 'always',
    meta: { persist: false },
  });
}

type QC = ReturnType<typeof useQueryClient>;

/** Marks items read in every cached inbox page and lowers the badge by how many were unread. */
function markReadInCache(qc: QC, isTarget: (n: AppNotification) => boolean) {
  let changed = 0;
  qc.setQueryData<InfiniteData<Paged<AppNotification>>>(notificationKeys.list, (data) =>
    data
      ? {
          ...data,
          pages: data.pages.map((page) => ({
            ...page,
            items: page.items.map((n) => {
              if (n.read || !isTarget(n)) return n;
              changed++;
              return { ...n, read: true };
            }),
          })),
        }
      : data,
  );
  return changed;
}

/** Plain call for code outside React (tapping a push). */
export function markNotificationRead(id: string) {
  return api<void>(`/api/notifications/${id}/read`, { method: 'POST' });
}

export function useMarkRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: markNotificationRead,
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: notificationKeys.all });
      const changed = markReadInCache(qc, (n) => n.id === id);
      if (changed) qc.setQueryData<{ count: number }>(notificationKeys.unreadCount, (c) => (c ? { count: Math.max(0, c.count - changed) } : c));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}

export function useMarkAllRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api<void>('/api/notifications/read-all', { method: 'POST' }),
    onMutate: async () => {
      await qc.cancelQueries({ queryKey: notificationKeys.all });
      markReadInCache(qc, () => true);
      qc.setQueryData<{ count: number }>(notificationKeys.unreadCount, { count: 0 });
    },
    onSettled: () => qc.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: notificationKeys.preferences,
    queryFn: ({ signal }) => api<NotificationPreferences>('/api/notifications/preferences', { signal }),
    meta: { persist: false },
  });
}

/** Optimistic: the switch flips at once and flips back if the save fails. */
export function useUpdateNotificationPreferences() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (prefs: NotificationPreferences) => api<NotificationPreferences>('/api/notifications/preferences', { method: 'PUT', body: prefs }),
    onMutate: async (prefs) => {
      await qc.cancelQueries({ queryKey: notificationKeys.preferences });
      const previous = qc.getQueryData<NotificationPreferences>(notificationKeys.preferences);
      qc.setQueryData(notificationKeys.preferences, prefs);
      return { previous };
    },
    onSuccess: (saved) => qc.setQueryData(notificationKeys.preferences, saved),
    onError: (_e, _prefs, context) => context?.previous && qc.setQueryData(notificationKeys.preferences, context.previous),
  });
}

/** Upserts this device's Expo push token for the signed-in user (moves it over if another user had it). */
export function registerPushDevice(token: string, platform: PushPlatform) {
  return api<void>('/api/notifications/devices', { method: 'PUT', body: { token, platform } });
}

export function unregisterPushDevice(token: string) {
  return api<void>(`/api/notifications/devices/${encodeURIComponent(token)}`, { method: 'DELETE' });
}
