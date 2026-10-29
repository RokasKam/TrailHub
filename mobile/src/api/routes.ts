import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api, apiFetch } from '@/lib/api';
import type { Accommodation, PlanDifficulty, PlannedRoute, Place, Poi, PoiKind, RouteSummary, RouteWaypoint } from '@/lib/types';

export const routeKeys = {
  all: ['routes'] as const,
  detail: (id: string) => ['routes', id] as const,
};

export function useRoutes() {
  return useQuery({ queryKey: routeKeys.all, queryFn: ({ signal }) => api<RouteSummary[]>('/api/routes', { signal }) });
}

export function useRoute(id: string | null) {
  return useQuery({ queryKey: routeKeys.detail(id ?? ''), queryFn: ({ signal }) => api<PlannedRoute>(`/api/routes/${id}`, { signal }), enabled: !!id });
}

const waypointKey = (w: RouteWaypoint[]) => w.map((p) => `${p.latitude.toFixed(5)},${p.longitude.toFixed(5)},${p.isOvernight ? 1 : 0}`).join(';');

/**
 * The routed line for the waypoints being edited. Keeps showing the previous line while the next one loads, so
 * the map doesn't flicker as points are added. Not persisted: previews are throwaway.
 */
export function useRoutePreview(waypoints: RouteWaypoint[], enabled = true) {
  return useQuery({
    queryKey: ['routes', 'preview', waypointKey(waypoints)],
    queryFn: ({ signal }) => api<PlannedRoute>('/api/routes/preview', { method: 'POST', body: { waypoints }, signal }),
    enabled: enabled && waypoints.length >= 2,
    placeholderData: keepPreviousData,
    staleTime: Infinity,
    gcTime: 5 * 60 * 1000,
    meta: { persist: false },
  });
}

export function useSaveRoute(id: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string | null; waypoints: RouteWaypoint[] }) =>
      id ? api<PlannedRoute>(`/api/routes/${id}`, { method: 'PUT', body }) : api<PlannedRoute>('/api/routes', { method: 'POST', body }),
    onSuccess: (route) => {
      qc.setQueryData(routeKeys.detail(route.id!), route);
      void qc.invalidateQueries({ queryKey: routeKeys.all, exact: true });
    },
  });
}

export function useDeleteRoute() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api<void>(`/api/routes/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: routeKeys.all, exact: true }),
  });
}

export function useAlternatives() {
  return useMutation({
    mutationFn: ({ from, to }: { from: RouteWaypoint; to: RouteWaypoint }) => api<PlannedRoute[]>('/api/routes/alternatives', { method: 'POST', body: { from, to } }),
  });
}

export type PlanRequest = {
  start: RouteWaypoint;
  finish: RouteWaypoint;
  via?: RouteWaypoint[];
  days: number;
  maxDailyDistanceKm: number | null;
  accommodation: Accommodation;
  difficulty: PlanDifficulty;
};

export function usePlanItinerary() {
  return useMutation({ mutationFn: (body: PlanRequest) => api<PlannedRoute>('/api/routes/plan', { method: 'POST', body }) });
}

/** Points of interest along a line; the line is thinned to a point every ~200 m before sending. */
export function usePoisAlong(path: [number, number, number | null][] | undefined, kinds: PoiKind[], corridorMeters = 500) {
  const line = thin(path ?? []);
  return useQuery({
    queryKey: ['routes', 'pois', kinds.join(','), corridorMeters, line.length, line[0]?.join(), line[line.length - 1]?.join()],
    queryFn: ({ signal }) => api<Poi[]>('/api/routes/pois', { method: 'POST', body: { line, kinds, corridorMeters }, signal }),
    enabled: line.length >= 2 && kinds.length > 0,
    placeholderData: keepPreviousData,
    staleTime: 24 * 60 * 60 * 1000,
  });
}

export function usePlaceSearch(text: string, near?: { latitude: number; longitude: number } | null) {
  const q = text.trim();
  return useQuery({
    queryKey: ['routes', 'places', q, near?.latitude.toFixed(1), near?.longitude.toFixed(1)],
    queryFn: ({ signal }) => api<Place[]>('/api/routes/places', { query: { q, lat: near?.latitude, lon: near?.longitude }, signal }),
    enabled: q.length >= 2,
    staleTime: 60 * 60 * 1000,
  });
}

export async function downloadRouteGpx(id: string) {
  return (await apiFetch(`/api/routes/${id}/gpx`, { query: { pois: true } })).text();
}

function thin(path: [number, number, number | null][], spacingMeters = 200) {
  const out: [number, number][] = [];
  let run = Infinity;
  for (let i = 0; i < path.length; i++) {
    if (i > 0) run += approxMeters(path[i - 1], path[i]);
    if (run >= spacingMeters || i === path.length - 1) {
      out.push([path[i][0], path[i][1]]);
      run = 0;
    }
  }
  return out;
}

/** Equirectangular distance: plenty accurate for points ~200 m apart. */
export function approxMeters(a: readonly [number, number, ...unknown[]], b: readonly [number, number, ...unknown[]]) {
  const x = ((b[1] - a[1]) * Math.PI) / 180 * Math.cos((((a[0] + b[0]) / 2) * Math.PI) / 180);
  const y = ((b[0] - a[0]) * Math.PI) / 180;
  return Math.sqrt(x * x + y * y) * 6_371_000;
}
