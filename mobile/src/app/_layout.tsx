// Register the background location tasks; must run before anything else at startup.
import '@/location/group-sharing';
import '@/location/tracking';

import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { useColorScheme } from 'react-native';

import { SessionProvider, useSession } from '@/auth/session';
import { AnimatedSplashOverlay } from '@/components/animated-icon';
import { persistOptions, queryClient } from '@/lib/query-client';
import { useSyncStore } from '@/stores/sync-store';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const colorScheme = useColorScheme();
  return (
    <PersistQueryClientProvider
      client={queryClient}
      persistOptions={persistOptions}
      // Once the offline cache is restored, retry any hikes recorded while offline.
      onSuccess={() => void useSyncStore.getState().flush()}>
      <SessionProvider>
        <ThemeProvider value={colorScheme === 'dark' ? DarkTheme : DefaultTheme}>
          <AnimatedSplashOverlay />
          <RootStack />
        </ThemeProvider>
      </SessionProvider>
    </PersistQueryClientProvider>
  );
}

function RootStack() {
  const { status } = useSession();
  if (status === 'loading') return null;

  const signedIn = status === 'signedIn';
  return (
    <Stack screenOptions={{ headerBackButtonDisplayMode: 'minimal' }}>
      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="trail/[id]" options={{ title: '' }} />
        <Stack.Screen name="activity/[id]" options={{ title: 'Hike' }} />
        <Stack.Screen name="trips/index" options={{ title: 'Trips' }} />
        <Stack.Screen name="trips/[id]" options={{ title: 'Trip' }} />
        <Stack.Screen name="groups/index" options={{ title: 'Groups' }} />
        <Stack.Screen name="routes/plan" options={{ title: 'Multi-day planner' }} />
        <Stack.Screen name="moderation" options={{ title: 'Moderation' }} />
        <Stack.Screen name="gear/index" options={{ title: 'Gear lists' }} />
        <Stack.Screen name="gear/[id]" options={{ title: 'Gear list' }} />
        <Stack.Screen name="routes/[id]" options={{ title: 'Route' }} />
        <Stack.Screen name="groups/[id]" options={{ title: 'Group' }} />
      </Stack.Protected>
      <Stack.Protected guard={!signedIn}>
        <Stack.Screen name="sign-in" options={{ headerShown: false }} />
      </Stack.Protected>
    </Stack>
  );
}
