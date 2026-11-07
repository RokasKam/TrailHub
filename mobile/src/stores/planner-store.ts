import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import type { Accommodation, Place, PlanDifficulty, PlannedRoute } from '@/lib/types';

type PlannerState = {
  start: Place | null;
  finish: Place | null;
  via: Place[];
  days: number;
  maxDaily: number | null;
  accommodation: Accommodation;
  difficulty: PlanDifficulty;
  /** The last plan, kept while the app runs so leaving the screen doesn't lose it (not saved to disk: it can be large). */
  result: PlannedRoute | null;
  set: (patch: Partial<Omit<PlannerState, 'set' | 'reset'>>) => void;
  reset: () => void;
};

const initial = { start: null, finish: null, via: [], days: 4, maxDaily: 20, accommodation: 'Hut' as const, difficulty: 'Moderate' as const, result: null };

/** The multi-day planner's inputs, kept across visits (and app restarts) so a half-planned hike isn't lost. */
export const usePlannerStore = create<PlannerState>()(
  persist(
    (set) => ({
      ...initial,
      set: (patch) => set(patch),
      reset: () => set(initial),
    }),
    {
      name: 'multi-day-planner',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: ({ result: _result, set: _set, reset: _reset, ...inputs }) => inputs,
    },
  ),
);
