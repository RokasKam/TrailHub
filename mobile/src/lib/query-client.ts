import AsyncStorage from '@react-native-async-storage/async-storage';
import NetInfo from '@react-native-community/netinfo';
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister';
import { focusManager, onlineManager, type Query, QueryClient } from '@tanstack/react-query';
import { AppState, Platform } from 'react-native';

import { ApiError } from '@/lib/api';

const DAY = 24 * 60 * 60 * 1000;

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60 * 1000,
      // Cached data stays available offline for a week (trails you looked at before a hike).
      gcTime: 7 * DAY,
      retry: (count, error) => !(error instanceof ApiError && error.isClientError) && count < 2,
      networkMode: 'offlineFirst',
    },
    mutations: { networkMode: 'offlineFirst' },
  },
});

/** Persists the query cache to disk, so trail details, routes and stats survive app restarts without signal. */
export const queryPersister = createAsyncStoragePersister({ storage: AsyncStorage, key: 'trailhub-query-cache', throttleTime: 2000 });

export const persistOptions = {
  persister: queryPersister,
  maxAge: 7 * DAY,
  // Bump when DTO shapes change so stale caches are dropped instead of crashing screens.
  buster: 'v2', // v2: trips gained group sharing (members, checklist kinds)
  dehydrateOptions: {
    // The default keeps only status 'success'. A refetch that fails offline flips a query to 'error' while it
    // still holds data, so the default would drop exactly the cached trails you need without signal.
    shouldDehydrateQuery: (query: Query) => query.state.data !== undefined,
  },
};

// Refetch stale queries when the app returns to the foreground.
if (Platform.OS !== 'web') {
  AppState.addEventListener('change', (status) => focusManager.setFocused(status === 'active'));
}

// Real connectivity instead of assuming online; paused queries/mutations resume on reconnect.
onlineManager.setEventListener((setOnline) =>
  NetInfo.addEventListener((state) => setOnline(state.isConnected !== false && state.isInternetReachable !== false)),
);
