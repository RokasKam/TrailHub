import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { tripKeys } from '@/api/planning';
import { api } from '@/lib/api';
import type { GearFacts, GearItem, GearList, GearSuggestion, PlannedRoute, Trip } from '@/lib/types';

export const gearKeys = { all: ['gear-lists'] as const };

/** Your gear lists, then the starter lists. */
export function useGearLists() {
  return useQuery({ queryKey: gearKeys.all, queryFn: ({ signal }) => api<GearList[]>('/api/trips/gear-lists', { signal }) });
}

export function useSaveGearList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, name, items }: { id: string | null; name: string; items: GearItem[] }) =>
      id ? api<GearList>(`/api/trips/gear-lists/${id}`, { method: 'PUT', body: { name, items } }) : api<GearList>('/api/trips/gear-lists', { method: 'POST', body: { name, items } }),
    onSuccess: () => qc.invalidateQueries({ queryKey: gearKeys.all }),
  });
}

export function useDeleteGearList() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/trips/gear-lists/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: gearKeys.all }),
  });
}

/** Trip-side actions: add a list or picked items to the checklist, or save the checklist as a list. */
export function useTripGear(tripId: string) {
  const qc = useQueryClient();
  const updated = (trip: Trip) => {
    qc.setQueryData(tripKeys.detail(tripId), trip);
    void qc.invalidateQueries({ queryKey: ['trips', tripId, 'gear-suggestions'] });
  };
  return {
    addList: useMutation({
      mutationFn: (listId: string) => api<Trip>(`/api/trips/${tripId}/checklist/from-list/${encodeURIComponent(listId)}`, { method: 'POST' }),
      onSuccess: updated,
    }),
    addItems: useMutation({
      mutationFn: (items: GearItem[]) => api<Trip>(`/api/trips/${tripId}/checklist/bulk`, { method: 'POST', body: { items } }),
      onSuccess: updated,
    }),
    saveAsList: useMutation({
      mutationFn: (name: string) => api<GearList>(`/api/trips/${tripId}/checklist/save-as-list`, { method: 'POST', body: { name } }),
      onSuccess: () => qc.invalidateQueries({ queryKey: gearKeys.all }),
    }),
  };
}

/** What the trip's planned route says about nights, altitude and the longest day, for better suggestions. */
export function gearFactsFrom(route: PlannedRoute | undefined): GearFacts {
  if (!route) return {};
  const nights = route.days.map((d) => d.overnight?.kind).filter((k) => k !== undefined);
  return {
    tentNights: nights.filter((k) => k === 'Campsite').length,
    hutNights: nights.filter((k) => k === 'MountainHut' || k === 'Shelter').length,
    hotelNights: nights.filter((k) => k === 'Hotel').length,
    maxElevationMeters: route.stats.maxElevationMeters,
    longestDayMinutes: Math.max(...route.days.map((d) => d.stats.durationMinutes)),
  };
}

export function useGearSuggestions(tripId: string, facts: GearFacts, checklistSize: number) {
  return useQuery({
    queryKey: ['trips', tripId, 'gear-suggestions', facts, checklistSize],
    queryFn: ({ signal }) => api<GearSuggestion[]>(`/api/trips/${tripId}/gear-suggestions`, { method: 'POST', body: facts, signal }),
    meta: { persist: false },
  });
}
