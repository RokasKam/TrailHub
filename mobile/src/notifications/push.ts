import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { AndroidImportance, IosAuthorizationStatus } from 'expo-notifications';
import { Alert, Linking, Platform } from 'react-native';
import { create } from 'zustand';

import { registerPushDevice, unregisterPushDevice } from '@/api/notifications';

/**
 * Push notifications: permission, the Expo push token and its registration with the backend.
 * The in-app inbox works without any of this; push is an extra that is skipped quietly whenever it can't work
 * (no EAS project id yet, Android without Firebase, an emulator without Google Play, permission denied).
 */

const TOKEN_KEY = 'push-token';
const CHANNEL_ID = 'default';

// Pushes that arrive while the app is open still show as a banner (and in Notification Center).
Notifications.setNotificationHandler({
  handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: true }),
});

/** `blocked`: denied and the OS won't show the prompt again; only Settings can change it. */
export type PushPermission = 'unknown' | 'granted' | 'undetermined' | 'blocked';

export const usePushPermission = create<{ status: PushPermission }>(() => ({ status: 'unknown' }));

function toPermission(p: Notifications.NotificationPermissionsStatus): PushPermission {
  if (p.granted || p.ios?.status === IosAuthorizationStatus.PROVISIONAL) return 'granted';
  return p.canAskAgain ? 'undetermined' : 'blocked';
}

export async function refreshPushPermission(): Promise<PushPermission> {
  if (Platform.OS === 'web') return 'blocked';
  const status = toPermission(await Notifications.getPermissionsAsync());
  usePushPermission.setState({ status });
  return status;
}

/** EAS project id from app.config.ts (`extra.eas.projectId`); missing until `eas init` has been run. */
function projectId(): string | undefined {
  return Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId ?? undefined;
}

/**
 * Real devices always; iOS simulators from iOS 16 (Xcode 14+); Android emulators only with Google Play services,
 * which can't be detected up front, so there the token request is simply tried and a failure ignored.
 */
function deviceSupportsPush() {
  if (Platform.OS === 'web') return false;
  if (Device.isDevice || Platform.OS === 'android') return true;
  return Number.parseInt(Device.osVersion ?? '0', 10) >= 16;
}

/** Android 13+ only shows the permission prompt once a channel exists, and pushes go to this one. */
async function ensureChannel() {
  if (Platform.OS !== 'android') return;
  await Notifications.setNotificationChannelAsync(CHANNEL_ID, {
    name: 'Notifications',
    importance: AndroidImportance.HIGH,
    lightColor: '#1F7A4D',
  });
}

let registering: Promise<void> | null = null;

/**
 * Registers this device for pushes if permission is already granted. Never prompts and never throws: called on
 * app start, after a permission grant and when the OS rotates the token.
 */
export function registerForPush(): Promise<void> {
  registering ??= register().finally(() => {
    registering = null;
  });
  return registering;
}

async function register() {
  try {
    if (!deviceSupportsPush()) return;
    if ((await refreshPushPermission()) !== 'granted') return;
    await ensureChannel();
    const id = projectId();
    if (!id) return; // no EAS project yet: inbox only
    const { data: token } = await Notifications.getExpoPushTokenAsync({ projectId: id });
    await registerPushDevice(token, Platform.OS === 'ios' ? 'ios' : 'android');
    await AsyncStorage.setItem(TOKEN_KEY, token);
  } catch (e) {
    // Offline, no Firebase config on Android, an emulator without Google Play… the next app start retries.
    if (__DEV__) console.warn('Push registration skipped:', e instanceof Error ? e.message : e);
  }
}

/** Removes this device's token from the backend. Call before signing out, while the request can still authenticate. */
export async function unregisterForPush() {
  try {
    const token = await AsyncStorage.getItem(TOKEN_KEY);
    if (Platform.OS !== 'web') void Notifications.setBadgeCountAsync(0).catch(() => {});
    if (!token) return;
    await AsyncStorage.removeItem(TOKEN_KEY);
    // Don't hold up sign-out when offline; the backend also drops tokens Expo reports as unregistered.
    await Promise.race([unregisterPushDevice(token), new Promise((resolve) => setTimeout(resolve, 4000))]);
  } catch {
    // Best effort.
  }
}

function confirm(title: string, message: string, ok: string) {
  return new Promise<boolean>((resolve) =>
    Alert.alert(title, message, [
      { text: 'Not now', style: 'cancel', onPress: () => resolve(false) },
      { text: ok, onPress: () => resolve(true) },
    ], { cancelable: true, onDismiss: () => resolve(false) }),
  );
}

/**
 * Asks for permission from a user action (the inbox card, a Profile switch): a short explanation first, then the
 * system prompt. When the OS won't prompt again, offers Settings instead. Resolves to whether pushes are allowed.
 */
export async function requestPushPermission(): Promise<boolean> {
  if (Platform.OS === 'web') return false;
  const current = await refreshPushPermission();
  if (current === 'granted') {
    void registerForPush();
    return true;
  }
  if (current === 'blocked') {
    if (await confirm('Notifications are off', 'Turn on notifications for TrailHub in Settings to hear about likes, group hikes and trip expenses.', 'Open Settings')) {
      void Linking.openSettings();
    }
    return false;
  }
  const go = await confirm(
    'Stay in the loop',
    'TrailHub can let you know when someone likes or comments on your hike, a group hike is scheduled, or a trip expense involves you. You can choose which in Profile.',
    'Continue',
  );
  if (!go) return false;
  await ensureChannel();
  const status = toPermission(await Notifications.requestPermissionsAsync());
  usePushPermission.setState({ status });
  if (status === 'granted') void registerForPush();
  return status === 'granted';
}

const PROMPTED_KEY = 'push-prompted';

/** The first time the inbox opens, ask once (with the explanation) if the user hasn't decided yet. */
export async function promptForPushOnce() {
  try {
    if (Platform.OS === 'web' || (await AsyncStorage.getItem(PROMPTED_KEY))) return;
    if ((await refreshPushPermission()) !== 'undetermined') return;
    await AsyncStorage.setItem(PROMPTED_KEY, '1');
    await requestPushPermission();
  } catch {
    // Best effort; the inbox card still offers it.
  }
}
