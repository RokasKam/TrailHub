import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { File } from 'expo-file-system';

import { api, apiFetch } from '@/lib/api';
import type {
  ConditionReport,
  Difficulty,
  ElevationSample,
  Paged,
  Review,
  TrailCondition,
  TrailDetails,
  TrailRoute,
  TrailSort,
  TrailSummary,
} from '@/lib/types';

export type TrailFilters = {
  q?: string;
  lat?: number;
  lon?: number;
  radiusKm?: number;
  difficulty?: Difficulty[];
  minDistanceKm?: number;
  maxDistanceKm?: number;
  minElevationGain?: number;
  maxElevationGain?: number;
  maxDurationMinutes?: number;
  sort?: TrailSort;
  minLat?: number;
  minLon?: number;
  maxLat?: number;
  maxLon?: number;
};

export const trailKeys = {
  all: ['trails'] as const,
  search: (filters: TrailFilters) => ['trails', 'search', filters] as const,
  detail: (id: string) => ['trails', id] as const,
  route: (id: string, maxPoints?: number) => ['trails', id, 'route', maxPoints ?? 'full'] as const,
  elevation: (id: string) => ['trails', id, 'elevation'] as const,
  reviews: (id: string) => ['trails', id, 'reviews'] as const,
  favorites: ['trails', 'favorites'] as const,
};

export function useTrailSearch(filters: TrailFilters) {
  return useQuery({
    queryKey: trailKeys.search(filters),
    queryFn: ({ signal }) => api<Paged<TrailSummary>>('/api/trails', { query: { ...filters, pageSize: 50 }, signal }),
    placeholderData: keepPreviousData,
  });
}

export function useTrail(id: string) {
  return useQuery({ queryKey: trailKeys.detail(id), queryFn: ({ signal }) => api<TrailDetails>(`/api/trails/${id}`, { signal }) });
}

export function useTrailRoute(id: string | null, maxPoints?: number) {
  return useQuery({
    enabled: id != null,
    queryKey: trailKeys.route(id ?? '', maxPoints),
    queryFn: ({ signal }) => api<TrailRoute>(`/api/trails/${id}/route`, { query: { maxPoints }, signal }),
    staleTime: Infinity, // routes don't change once imported
  });
}

export function useElevationProfile(id: string) {
  return useQuery({
    queryKey: trailKeys.elevation(id),
    queryFn: ({ signal }) => api<ElevationSample[]>(`/api/trails/${id}/elevation-profile`, { query: { samples: 120 }, signal }),
    staleTime: Infinity,
  });
}

export function useFavoriteTrails() {
  return useQuery({ queryKey: trailKeys.favorites, queryFn: ({ signal }) => api<TrailSummary[]>('/api/trails/favorites', { signal }) });
}

export function useToggleFavorite(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (favorite: boolean) => api<void>(`/api/trails/${id}/favorite`, { method: favorite ? 'PUT' : 'DELETE' }),
    onMutate: async (favorite) => {
      await qc.cancelQueries({ queryKey: trailKeys.detail(id) });
      qc.setQueryData<TrailDetails>(trailKeys.detail(id), (t) => (t ? { ...t, isFavorite: favorite } : t));
    },
    onSettled: () => {
      void qc.invalidateQueries({ queryKey: trailKeys.detail(id) });
      void qc.invalidateQueries({ queryKey: trailKeys.favorites });
    },
  });
}

export function useReviews(id: string) {
  return useQuery({ queryKey: trailKeys.reviews(id), queryFn: ({ signal }) => api<Paged<Review>>(`/api/trails/${id}/reviews`, { signal }) });
}

export function useSubmitReview(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { rating: number; comment?: string }) => api<Review>(`/api/trails/${id}/reviews/mine`, { method: 'PUT', body }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: trailKeys.reviews(id) });
      void qc.invalidateQueries({ queryKey: trailKeys.detail(id) });
    },
  });
}

export function useReportCondition(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { condition: TrailCondition; note?: string }) => api<ConditionReport>(`/api/trails/${id}/conditions`, { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: trailKeys.detail(id) }),
  });
}

export function useImportTrailGpx() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { uri: string; fileName: string; region: string; difficulty: Difficulty; name?: string }) => {
      const form = new FormData();
      // Expo's fetch (SDK 52+) only accepts real Blob parts; expo-file-system's File is one.
      form.append('file', new File(input.uri), input.fileName);
      form.append('region', input.region);
      form.append('difficulty', input.difficulty);
      if (input.name) form.append('name', input.name);
      return api<{ id: string }>('/api/trails/import', { method: 'POST', body: form });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: trailKeys.all }),
  });
}

export function useUploadTrailPhoto(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { uri: string; fileName: string; caption?: string }) => {
      const form = new FormData();
      // The resized photo is a .jpg, so File reports "image/jpeg", which the server requires.
      form.append('file', new File(input.uri), input.fileName);
      if (input.caption) form.append('caption', input.caption);
      return api<{ url: string; caption: string | null; uploadedAtUtc: string }>(`/api/trails/${id}/photos/upload`, { method: 'POST', body: form });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: trailKeys.detail(id) }),
  });
}

/** Raw GPX text, for saving/sharing a file. */
export async function downloadTrailGpx(id: string) {
  return (await apiFetch(`/api/trails/${id}/gpx`)).text();
}
