import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { Achievement, Preferences, Profile, Units } from '@/lib/types';

export const profileKeys = {
  me: ['profile', 'me'] as const,
  achievements: ['profile', 'achievements'] as const,
};

export function useProfile() {
  return useQuery({ queryKey: profileKeys.me, queryFn: ({ signal }) => api<Profile>('/api/profiles/me', { signal }) });
}

/** The user's unit preference, defaulting to metric while the profile loads or offline with no cache. */
export function useUnits(): Units {
  return useProfile().data?.preferences.units ?? 'Metric';
}

export function useAchievements() {
  return useQuery({ queryKey: profileKeys.achievements, queryFn: ({ signal }) => api<Achievement[]>('/api/profiles/me/achievements', { signal }) });
}

export function useUpdatePreferences() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (preferences: Preferences) => api<Profile>('/api/profiles/me/preferences', { method: 'PUT', body: preferences }),
    onMutate: async (preferences) => {
      await qc.cancelQueries({ queryKey: profileKeys.me });
      qc.setQueryData<Profile>(profileKeys.me, (p) => (p ? { ...p, preferences } : p));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: profileKeys.me }),
  });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { displayName: string; bio?: string | null; homeRegion?: string | null; avatarUrl?: string | null }) =>
      api<Profile>('/api/profiles/me', { method: 'PUT', body }),
    onSuccess: (profile) => qc.setQueryData(profileKeys.me, profile),
  });
}
