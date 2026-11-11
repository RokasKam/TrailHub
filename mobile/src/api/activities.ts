import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { File } from 'expo-file-system';

import { api, apiFetch } from '@/lib/api';
import type { ActivityDetails, ActivitySummary, HikePhoto, HikeSharing, Paged, ShareTarget, Statistics, TrackPrivacy } from '@/lib/types';

export const activityKeys = {
  all: ['activities'] as const,
  list: ['activities', 'list'] as const,
  detail: (id: string) => ['activities', id] as const,
  stats: ['activities', 'statistics'] as const,
  shareTargets: ['activities', 'share-targets'] as const,
};

export function useActivities() {
  return useInfiniteQuery({
    queryKey: activityKeys.list,
    queryFn: ({ pageParam, signal }) => api<Paged<ActivitySummary>>('/api/activities', { query: { page: pageParam, pageSize: 20 }, signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });
}

export function useActivity(id: string) {
  return useQuery({ queryKey: activityKeys.detail(id), queryFn: ({ signal }) => api<ActivityDetails>(`/api/activities/${id}`, { signal }) });
}

export function useStatistics() {
  return useQuery({ queryKey: activityKeys.stats, queryFn: ({ signal }) => api<Statistics>('/api/activities/statistics', { signal }) });
}

export function useDeleteActivity() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/activities/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: activityKeys.all });
      void qc.invalidateQueries({ queryKey: ['profile'] });
    },
  });
}

export function useImportActivityGpx() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { uri: string; fileName: string; name?: string }) => {
      const form = new FormData();
      // Expo's fetch (SDK 52+) only accepts real Blob parts; expo-file-system's File is one.
      form.append('file', new File(input.uri), input.fileName);
      if (input.name) form.append('name', input.name);
      return api<ActivitySummary>('/api/activities/import', { method: 'POST', body: form });
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: activityKeys.all });
      void qc.invalidateQueries({ queryKey: ['profile'] });
    },
  });
}

export async function downloadActivityGpx(id: string) {
  return (await apiFetch(`/api/activities/${id}/gpx`)).text();
}

// Sharing a hike with groups and its photos (author only). Changes show up in the feed, so it's refreshed too.

/** The caller's groups a hike can be shared with. */
export function useShareTargets() {
  return useQuery({
    queryKey: activityKeys.shareTargets,
    queryFn: ({ signal }) => api<ShareTarget[]>('/api/activities/share-targets', { signal }),
    meta: { persist: false },
  });
}

function useHikeChanged(id: string) {
  const qc = useQueryClient();
  // Returned promise: mutateAsync callers (photo uploads) wait until the hike shows the change.
  return () => Promise.all([qc.invalidateQueries({ queryKey: activityKeys.detail(id) }), qc.invalidateQueries({ queryKey: ['feed'] })]);
}

export function useSaveSharing(id: string) {
  const changed = useHikeChanged(id);
  return useMutation({
    mutationFn: (body: { groupIds: string[]; caption: string | null; privacy: TrackPrivacy }) =>
      api<HikeSharing>(`/api/activities/${id}/sharing`, { method: 'PUT', body }),
    onSuccess: changed,
  });
}

export function useStopSharing(id: string) {
  const changed = useHikeChanged(id);
  return useMutation({ mutationFn: () => api<void>(`/api/activities/${id}/sharing`, { method: 'DELETE' }), onSuccess: changed });
}

export const MAX_HIKE_PHOTOS = 6;

export function useUploadHikePhoto(id: string) {
  const changed = useHikeChanged(id);
  return useMutation({
    mutationFn: async (input: { uri: string; fileName: string; caption?: string }) => {
      const form = new FormData();
      // The resized photo is a .jpg, so File reports "image/jpeg".
      form.append('file', new File(input.uri), input.fileName);
      if (input.caption) form.append('caption', input.caption);
      return api<HikePhoto>(`/api/activities/${id}/photos`, { method: 'POST', body: form });
    },
    onSuccess: changed,
  });
}

export function useDeleteHikePhoto(id: string) {
  const changed = useHikeChanged(id);
  return useMutation({ mutationFn: (photoId: string) => api<void>(`/api/activities/${id}/photos/${photoId}`, { method: 'DELETE' }), onSuccess: changed });
}
