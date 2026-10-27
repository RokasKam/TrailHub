import type { ConfigContext, ExpoConfig } from 'expo/config';

// Native config needs the Auth0 domain at build time (callback URL scheme), so this reads the
// same EXPO_PUBLIC_* variables the JS code uses. Change them → rebuild the dev client.
const auth0Domain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN || 'example.auth0.com';
const bundleId = process.env.APP_BUNDLE_ID || 'app.trailhub.mobile';

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: 'TrailHub',
  slug: 'trailhub',
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
  ],
  experiments: { typedRoutes: true, reactCompiler: true },
});
