import type { ConfigContext, ExpoConfig } from 'expo/config';

// Native config needs the Auth0 domain at build time (callback URL scheme), so this reads the
// same EXPO_PUBLIC_* variables the JS code uses. Change them → rebuild the dev client.
const auth0Domain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN || 'example.auth0.com';
const bundleId = process.env.APP_BUNDLE_ID || 'app.trailhub.mobile';

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: 'TrailHub',
  slug: 'trailhub',
  owner: 'rokasss9',
  version: '1.0.0',
  orientation: 'portrait',
  icon: './assets/images/icon.png',
  scheme: 'trailhub',
  userInterfaceStyle: 'automatic',
  platforms: ['ios', 'android'],
  ios: {
    icon: './assets/expo.icon',
    bundleIdentifier: bundleId,
    supportsTablet: true,
    infoPlist: {
      // GPX files from other apps (Files, Mail) can be opened with the document picker.
      LSSupportsOpeningDocumentsInPlace: true,
    },
  },
  android: {
    package: bundleId,
    adaptiveIcon: {
      backgroundColor: '#E6F4FE',
      foregroundImage: './assets/images/android-icon-foreground.png',
      backgroundImage: './assets/images/android-icon-background.png',
      monochromeImage: './assets/images/android-icon-monochrome.png',
    },
    predictiveBackGestureEnabled: false,
    // Android push tokens need Firebase (FCM). Point this at the google-services.json from the Firebase console
    // (e.g. GOOGLE_SERVICES_FILE=./google-services.json); without it Android simply skips push registration.
    ...(process.env.GOOGLE_SERVICES_FILE && { googleServicesFile: process.env.GOOGLE_SERVICES_FILE }),
  },
  plugins: [
    'expo-router',
    ['expo-splash-screen', { backgroundColor: '#2F7D4F', image: './assets/images/splash-icon.png', imageWidth: 76 }],
    'expo-dev-client',
    '@rnmapbox/maps',
    // Listed before react-native-auth0 on purpose: Expo applies AppDelegate mods in reverse order,
    // so this runs after Auth0's plugin and can strip what it inserted.
    './plugins/with-auth0-sdk58-fix',
    // customScheme must match config.auth0.customScheme in src/config.ts.
    ['react-native-auth0', { domain: auth0Domain, customScheme: 'trailhub.auth0' }],
    [
      'expo-location',
      {
        locationWhenInUsePermission: 'TrailHub uses your location to show nearby trails and record your hikes.',
        locationAlwaysAndWhenInUsePermission: 'Allow "Always" so TrailHub keeps recording your hike when the screen is off.',
        isIosBackgroundLocationEnabled: true,
        isAndroidBackgroundLocationEnabled: true,
        isAndroidForegroundServiceEnabled: true,
      },
    ],
    'expo-task-manager',
    [
      'expo-image-picker',
      {
        photosPermission: 'TrailHub lets you add photos of trails from your library.',
        cameraPermission: 'TrailHub lets you take photos of trails to share with other hikers.',
        microphonePermission: false,
      },
    ],
    '@react-native-community/datetimepicker',
    'expo-sharing',
    'expo-secure-store',
    // Android: tint the tray icon with the brand green; FCM messages without a channel land in "default"
    // (created at runtime in src/notifications/push.ts with high importance).
    ['expo-notifications', { color: '#1F7A4D', defaultChannel: 'default' }],
  ],
  experiments: { typedRoutes: true, reactCompiler: true },
  extra: {
    ...config.extra,
    // Expo push tokens need the EAS project id (not a secret). EAS_PROJECT_ID overrides it, e.g. for a fork with its
    // own EAS project. Without one the app still works: the inbox loads and push registration is skipped.
    eas: { ...config.extra?.eas, projectId: process.env.EAS_PROJECT_ID ?? '37e8ee29-45e8-46e3-b913-e449a38c3776' },
  },
});
