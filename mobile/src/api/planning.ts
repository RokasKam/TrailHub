import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ChecklistItemKind, Forecast, Group, GroupHike, GroupSummary, LiveLocation, RsvpStatus, Trip, TripSummary } from '@/lib/types';

// Weather ---------------------------------------------------------------------------------

export function useForecast(point: { latitude: number; longitude: number; elevation?: number | null } | undefined, range?: { from?: string; to?: string }) {
  return useQuery({
    queryKey: ['weather', point?.latitude.toFixed(2), point?.longitude.toFixed(2), point?.elevation ?? null, range?.from, range?.to],
    queryFn: ({ signal }) =>
      api<Forecast>('/api/weather', {
        query: { lat: point!.latitude, lon: point!.longitude, elevation: point?.elevation ?? undefined, from: range?.from, to: range?.to },
        signal,
      }),
    enabled: !!point,
    staleTime: 15 * 60 * 1000,
  });
}

// Trips -----------------------------------------------------------------------------------

export const tripKeys = {
  all: ['trips'] as const,
  detail: (id: string) => ['trips', id] as const,
  forGroup: (groupId: string) => ['trips', 'group', groupId] as const,
};

/** My own trips plus trips shared with my groups. */
export function useTrips() {
  return useQuery({ queryKey: tripKeys.all, queryFn: ({ signal }) => api<TripSummary[]>('/api/trips', { signal }) });
}

export function useGroupTrips(groupId: string) {
  return useQuery({ queryKey: tripKeys.forGroup(groupId), queryFn: ({ signal }) => api<TripSummary[]>('/api/trips', { query: { groupId }, signal }) });
}

export function useTrip(id: string) {
  return useQuery({ queryKey: tripKeys.detail(id), queryFn: ({ signal }) => api<Trip>(`/api/trips/${id}`, { signal }) });
}

export function useCreateTrip() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string | null; startDate: string; endDate: string; groupId?: string | null }) =>
      api<Trip>('/api/trips', { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: tripKeys.all }),
  });
}

type TripChange =
  | { kind: 'reschedule'; startDate: string; endDate: string }
  | { kind: 'addTrail'; day: number; trailId: string }
  | { kind: 'removeTrail'; day: number; trailId: string }
  | { kind: 'updateDay'; day: number; notes: string | null; overnight: string | null }
  | { kind: 'share'; groupId: string | null }
  | { kind: 'attachRoute'; routeId: string | null }
  | { kind: 'addItem'; text: string; itemKind: ChecklistItemKind }
  | { kind: 'toggleItem'; itemId: string; isDone: boolean }
  | { kind: 'claimItem'; itemId: string; claim: boolean }
  | { kind: 'removeItem'; itemId: string }
  | { kind: 'complete' }
  | { kind: 'cancel' };

/** Every trip edit returns the updated trip, which replaces the cache entry directly. */
export function useTripChange(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (change: TripChange) => {
      const base = `/api/trips/${id}`;
      switch (change.kind) {
        case 'reschedule':
          return api<Trip>(`${base}/dates`, { method: 'PUT', body: { startDate: change.startDate, endDate: change.endDate } });
        case 'addTrail':
          return api<Trip>(`${base}/days/${change.day}/trails/${change.trailId}`, { method: 'PUT' });
        case 'removeTrail':
          return api<Trip>(`${base}/days/${change.day}/trails/${change.trailId}`, { method: 'DELETE' });
        case 'updateDay':
          return api<Trip>(`${base}/days/${change.day}`, { method: 'PUT', body: { notes: change.notes, overnight: change.overnight } });
        case 'attachRoute':
          return api<Trip>(`${base}/route`, { method: 'PUT', body: { routeId: change.routeId } });
        case 'share':
          return api<Trip>(`${base}/group`, { method: 'PUT', body: { groupId: change.groupId } });
        case 'addItem':
          return api<Trip>(`${base}/checklist`, { method: 'POST', body: { text: change.text, kind: change.itemKind } });
        case 'toggleItem':
          return api<Trip>(`${base}/checklist/${change.itemId}`, { method: 'PUT', body: { isDone: change.isDone } });
        case 'claimItem':
          return api<Trip>(`${base}/checklist/${change.itemId}/claim`, { method: 'PUT', body: { claim: change.claim } });
        case 'removeItem':
          return api<Trip>(`${base}/checklist/${change.itemId}`, { method: 'DELETE' });
        case 'complete':
          return api<Trip>(`${base}/complete`, { method: 'POST' });
        case 'cancel':
          return api<Trip>(`${base}/cancel`, { method: 'POST' });
      }
    },
    onSuccess: (trip) => {
      qc.setQueryData(tripKeys.detail(id), trip);
      void qc.invalidateQueries({ queryKey: tripKeys.all, exact: true });
      void qc.invalidateQueries({ queryKey: ['trips', 'group'] });
    },
    // Someone else changed it first (e.g. claimed the same item): show the latest state.
    onError: () => qc.invalidateQueries({ queryKey: tripKeys.detail(id) }),
  });
}

// Groups ----------------------------------------------------------------------------------

export const groupKeys = {
  all: ['groups'] as const,
  detail: (id: string) => ['groups', id] as const,
  locations: (id: string) => ['groups', id, 'locations'] as const,
};

export function useGroups() {
  return useQuery({ queryKey: groupKeys.all, queryFn: ({ signal }) => api<GroupSummary[]>('/api/groups', { signal }) });
}

export function useGroup(id: string) {
  return useQuery({ queryKey: groupKeys.detail(id), queryFn: ({ signal }) => api<Group>(`/api/groups/${id}`, { signal }) });
}

export function useLiveLocations(id: string) {
  return useQuery({ queryKey: groupKeys.locations(id), queryFn: ({ signal }) => api<LiveLocation[]>(`/api/groups/${id}/locations`, { signal }) });
}

export function useCreateGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string | null; displayName?: string | null }) => api<Group>('/api/groups', { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: groupKeys.all }),
  });
}

export function useJoinGroup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { inviteCode: string; displayName?: string | null }) => api<Group>('/api/groups/join', { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: groupKeys.all }),
  });
}

export function useScheduleGroupHike(groupId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      title: string;
      trailId?: string | null;
      scheduledAtUtc: string;
      meetingLatitude?: number | null;
      meetingLongitude?: number | null;
      notes?: string | null;
    }) =>
      api<GroupHike>(`/api/groups/${groupId}/hikes`, { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: groupKeys.detail(groupId) }),
  });
}

export function useRsvp(groupId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ hikeId, status }: { hikeId: string; status: RsvpStatus }) =>
      api<GroupHike>(`/api/groups/${groupId}/hikes/${hikeId}/rsvp`, { method: 'PUT', body: { status } }),
    onSuccess: (hike) =>
      qc.setQueryData<Group>(groupKeys.detail(groupId), (g) => (g ? { ...g, upcomingHikes: g.upcomingHikes.map((h) => (h.id === hike.id ? hike : h)) } : g)),
  });
}

export function useLeaveGroup(groupId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api<void>(`/api/groups/${groupId}/leave`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: groupKeys.all }),
  });
}
