import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { File } from 'expo-file-system';

import { api, apiFetch } from '@/lib/api';
import type { ActivityDetails, ActivitySummary, Paged, Statistics } from '@/lib/types';

export const activityKeys = {
  all: ['activities'] as const,
  list: ['activities', 'list'] as const,
  detail: (id: string) => ['activities', id] as const,
  stats: ['activities', 'statistics'] as const,
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
