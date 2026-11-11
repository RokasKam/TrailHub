import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { File } from 'expo-file-system';

import { api, apiFetch } from '@/lib/api';
import type {
  ConditionReport,
  ContentReportReason,
  ModerationAction,
  ModerationItem,
  PhotoModerationItem,
  ReportReason,
  Difficulty,
  ElevationSample,
  Paged,
  Review,
  TrailCondition,
  TrailDetails,
  TrailPhoto,
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
  /** Prefix of both moderation queues (trails and trail photos). */
  moderation: ['trails', 'moderation'] as const,
  photoModeration: ['trails', 'moderation', 'photos'] as const,
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
      return api<TrailPhoto>(`/api/trails/${id}/photos/upload`, { method: 'POST', body: form });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: trailKeys.detail(id) }),
  });
}

/** Raw GPX text, for saving/sharing a file. */
export async function downloadTrailGpx(id: string) {
  return (await apiFetch(`/api/trails/${id}/gpx`)).text();
}

export function useReportTrail(id: string) {
  return useMutation({
    mutationFn: (body: { reason: ReportReason; details?: string | null }) => api<void>(`/api/trails/${id}/report`, { method: 'POST', body }),
  });
}

/** Uploader, trail author or moderator. */
export function useDeleteTrailPhoto(trailId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (photoId: string) => api<void>(`/api/trails/${trailId}/photos/${photoId}`, { method: 'DELETE' }),
    onSuccess: (_r, photoId) => {
      qc.setQueryData<TrailDetails>(trailKeys.detail(trailId), (t) => (t ? { ...t, photos: t.photos.filter((p) => p.id !== photoId) } : t));
      void qc.invalidateQueries({ queryKey: trailKeys.detail(trailId) });
      void qc.invalidateQueries({ queryKey: trailKeys.photoModeration });
    },
  });
}

export function useReportTrailPhoto(trailId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ photoId, ...body }: { photoId: string; reason: ContentReportReason; details?: string | null }) =>
      api<void>(`/api/trails/${trailId}/photos/${photoId}/report`, { method: 'POST', body }),
    // A third report hides the photo, so the gallery may change.
    onSuccess: () => qc.invalidateQueries({ queryKey: trailKeys.detail(trailId) }),
  });
}

/** The moderation queue; fails with 403 for everyone who isn't a moderator, which is how the app knows. */
export function useModerationQueue() {
  return useQuery({
    queryKey: trailKeys.moderation,
    queryFn: ({ signal }) => api<ModerationItem[]>('/api/trails/moderation', { signal }),
    retry: false,
    meta: { persist: false },
  });
}

export function useModerateTrail() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, hidden }: { id: string; hidden: boolean }) => api<void>(`/api/trails/${id}/moderation`, { method: 'PUT', body: { hidden } }),
    onSuccess: (_r, { id }) => {
      void qc.invalidateQueries({ queryKey: trailKeys.moderation });
      void qc.invalidateQueries({ queryKey: trailKeys.detail(id) });
      void qc.invalidateQueries({ queryKey: ['trails', 'search'] });
    },
  });
}

/** Reported or hidden trail photos; 403 for everyone who isn't a moderator. */
export function usePhotoModerationQueue() {
  return useQuery({
    queryKey: trailKeys.photoModeration,
    queryFn: ({ signal }) => api<PhotoModerationItem[]>('/api/trails/moderation/photos', { signal }),
    retry: false,
    meta: { persist: false },
  });
}

export function useModerateTrailPhoto() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ trailId, photoId, action }: { trailId: string; photoId: string; action: ModerationAction }) =>
      api<void>(`/api/trails/${trailId}/photos/${photoId}/moderation`, { method: 'PUT', body: { action } }),
    onSuccess: (_r, { trailId }) => {
      void qc.invalidateQueries({ queryKey: trailKeys.photoModeration });
      void qc.invalidateQueries({ queryKey: trailKeys.detail(trailId) });
    },
  });
}
