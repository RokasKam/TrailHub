import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, type PropsWithChildren, useContext, useEffect, useMemo } from 'react';
import { Auth0Provider, useAuth0 } from 'react-native-auth0';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

import { config } from '@/config';
import { setAuthHeaderProvider } from '@/lib/api';
import { queryClient } from '@/lib/query-client';

export type Session = {
  status: 'loading' | 'signedOut' | 'signedIn';
  user: { id: string; name: string | null } | null;
  /**
   * Auth0 mode opens Universal Login (`signUp` jumps straight to its sign-up screen).
   * Development mode uses `name` as the user id.
   */
  signIn: (options?: { signUp?: boolean; name?: string }) => Promise<void>;
  signOut: () => Promise<void>;
};

const SessionContext = createContext<Session | null>(null);

export function useSession() {
  const session = useContext(SessionContext);
  if (!session) throw new Error('useSession must be used inside <SessionProvider>');
  return session;
}

export function SessionProvider({ children }: PropsWithChildren) {
  if (config.authMode === 'development') {
    return <DevelopmentSession>{children}</DevelopmentSession>;
  }

  return (
    <Auth0Provider domain={config.auth0.domain} clientId={config.auth0.clientId}>
      <Auth0Session>{children}</Auth0Session>
    </Auth0Provider>
  );
}

/**
 * Auth0 Universal Login (handles registration, social logins, password reset and MFA).
 * `offline_access` gets a refresh token, which the SDK stores in the Keychain/Keystore and uses
 * to renew the short-lived access token inside getCredentials().
 */
function Auth0Session({ children }: PropsWithChildren) {
  const { user, isLoading, authorize, clearSession, getCredentials } = useAuth0();

  useEffect(() => {
    setAuthHeaderProvider(async () => {
      try {
        const credentials = await getCredentials();
        return `Bearer ${credentials.accessToken}`;
      } catch {
        return null; // signed out or refresh token revoked; the API answers 401
      }
    });
  }, [getCredentials]);

  const session = useMemo<Session>(
    () => ({
      status: isLoading ? 'loading' : user ? 'signedIn' : 'signedOut',
      user: user ? { id: user.sub, name: user.name ?? user.nickname ?? null } : null,
      signIn: async (options) => {
        await authorize(
          {
            scope: 'openid profile email offline_access',
            audience: config.auth0.audience,
            additionalParameters: options?.signUp ? { screen_hint: 'signup' } : undefined,
          },
          // Without this the SDK falls back to "<bundle id>.auth0://" for the callback URL.
          { customScheme: config.auth0.customScheme },
        );
      },
      signOut: async () => {
        await clearSession({}, { customScheme: config.auth0.customScheme });
        queryClient.clear();
      },
    }),
    [authorize, clearSession, isLoading, user],
  );

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

const useDevUser = create<{ userId: string | null; hydrated: boolean; setUserId: (id: string | null) => void }>()(
  persist(
    (set) => ({ userId: null, hydrated: false, setUserId: (userId) => set({ userId }) }),
    {
      name: 'dev-session',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: (s) => ({ userId: s.userId }),
      onRehydrateStorage: () => () => useDevUser.setState({ hydrated: true }),
    },
  ),
);

/** Pairs with the backend's Auth:Mode=Development: sends `Authorization: Dev <id>`. Local use only. */
function DevelopmentSession({ children }: PropsWithChildren) {
  const { userId, hydrated, setUserId } = useDevUser();

  useEffect(() => {
    setAuthHeaderProvider(async () => {
      const id = useDevUser.getState().userId;
      return id ? `Dev ${id}` : null;
    });
  }, []);

  const session = useMemo<Session>(
    () => ({
      status: !hydrated ? 'loading' : userId ? 'signedIn' : 'signedOut',
      user: userId ? { id: userId, name: userId } : null,
      signIn: async (options) => setUserId((options?.name || 'dev-hiker').trim().toLowerCase().replace(/\s+/g, '-')),
      signOut: async () => {
        setUserId(null);
        queryClient.clear();
      },
    }),
    [hydrated, setUserId, userId],
  );

  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
