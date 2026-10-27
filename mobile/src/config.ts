// All values come from EXPO_PUBLIC_* env vars (see .env.example). They are inlined at build
// time and visible in the app bundle, so never put secrets here; Auth0 client ids and Mapbox
// public tokens are designed to be public.

export const config = {
  /** The API gateway. From a simulator, localhost works on iOS; Android emulators need 10.0.2.2. */
  apiUrl: process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:8080',

  /** "auth0" (default) or "development" (pairs with the backend's Auth:Mode=Development). */
  authMode: (process.env.EXPO_PUBLIC_AUTH_MODE ?? 'auth0') as 'auth0' | 'development',

  auth0: {
    domain: process.env.EXPO_PUBLIC_AUTH0_DOMAIN ?? '',
    clientId: process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID ?? '',
    audience: process.env.EXPO_PUBLIC_AUTH0_AUDIENCE ?? '',
    /** Must match `customScheme` in app.config.ts and the Auth0 Allowed Callback/Logout URLs. */
    customScheme: 'trailhub.auth0',
  },

  mapboxToken: process.env.EXPO_PUBLIC_MAPBOX_TOKEN ?? '',
};
