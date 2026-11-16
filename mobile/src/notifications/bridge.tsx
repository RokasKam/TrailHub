import { useQueryClient } from '@tanstack/react-query';
import * as Notifications from 'expo-notifications';
import { type Href, router } from 'expo-router';
import { useEffect } from 'react';
import { AppState, Platform } from 'react-native';

import { markNotificationRead, notificationKeys, useUnreadCount } from '@/api/notifications';
import { refreshPushPermission, registerForPush, usePushPermission } from '@/notifications/push';

/** Each tap is handled once, whether it arrives through the listener or as the cold-start "last response". */
const handled = new Set<string>();

function openFromPush(response: Notifications.NotificationResponse, onRead: () => void) {
  const key = response.notification.request.identifier;
  if (handled.has(key)) return;
  handled.add(key);
  if (response.actionIdentifier !== Notifications.DEFAULT_ACTION_IDENTIFIER) return;

  const data = response.notification.request.content.data ?? {};
  const url = typeof data.url === 'string' ? data.url : null;
  const notificationId = typeof data.notificationId === 'string' ? data.notificationId : null;
  if (notificationId) void markNotificationRead(notificationId).then(onRead, () => {});
  // Only in-app paths (e.g. /feed/{id}); anything else would leave the app or hit an unknown route.
  if (url?.startsWith('/')) router.push(url as Href);
}

/**
 * Signed-in side of push notifications, rendered once next to the root stack: re-registers the device on start
 * when permission is already granted, opens the target of a tapped push (also from a cold start), refreshes the
 * inbox when a push arrives and keeps the iOS app icon badge equal to the unread count.
 */
export function NotificationsBridge() {
  const qc = useQueryClient();
  const unread = useUnreadCount();
  const unreadCount = unread.data;
  const permission = usePushPermission((s) => s.status);

  useEffect(() => {
    const refreshInbox = () => void qc.invalidateQueries({ queryKey: notificationKeys.all });

    void registerForPush(); // no prompt: only registers when permission was granted before

    // Cold start: the app was launched by tapping a push.
    const last = Notifications.getLastNotificationResponse();
    if (last) {
      openFromPush(last, refreshInbox);
      Notifications.clearLastNotificationResponse();
    }

    const subscriptions = [
      Notifications.addNotificationResponseReceivedListener((response) => openFromPush(response, refreshInbox)),
      Notifications.addNotificationReceivedListener(refreshInbox),
      // The OS rotated the native token: the Expo token may follow, so register again.
      Notifications.addPushTokenListener(() => void registerForPush()),
      // Back from Settings with notifications newly allowed: register then.
      AppState.addEventListener('change', (state) => {
        if (state !== 'active') return;
        const before = usePushPermission.getState().status;
        void refreshPushPermission().then((after) => {
          if (after === 'granted' && before !== 'granted') void registerForPush();
        });
      }),
    ];
    return () => subscriptions.forEach((s) => s.remove());
  }, [qc]);

  useEffect(() => {
    if (Platform.OS !== 'ios' || unreadCount === undefined || permission !== 'granted') return;
    void Notifications.setBadgeCountAsync(unreadCount).catch(() => {});
  }, [permission, unreadCount]);

  return null;
}
