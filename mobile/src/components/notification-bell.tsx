import { router, useFocusEffect } from 'expo-router';
import { type ReactNode, useCallback } from 'react';
import { StyleSheet, View } from 'react-native';

import { useUnreadCount } from '@/api/notifications';
import { ThemedText } from '@/components/themed-text';
import { IconButton } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';

const BadgeRed = '#DC2626';

/** Header bell that opens the inbox, with the unread count on top. */
export function NotificationBell() {
  const theme = useTheme();
  const unread = useUnreadCount();
  const count = unread.data ?? 0;
  const { refetch } = unread;

  // Coming back to the tab (e.g. from the inbox or a hike) shows a fresh count.
  useFocusEffect(
    useCallback(() => {
      void refetch();
    }, [refetch]),
  );

  return (
    <View>
      <IconButton icon="bell" label={count ? `Notifications, ${count} unread` : 'Notifications'} onPress={() => router.push('/notifications')} />
      {count > 0 && (
        <View pointerEvents="none" style={[styles.badge, { backgroundColor: BadgeRed, borderColor: theme.background }]}>
          <ThemedText type="caption" style={styles.badgeText}>
            {count > 99 ? '99+' : count}
          </ThemedText>
        </View>
      )}
    </View>
  );
}

/** Lays out several header actions in a row (PageHeader takes one node). */
export function HeaderActions({ children }: { children: ReactNode }) {
  return <View style={styles.actions}>{children}</View>;
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  badge: {
    position: 'absolute',
    top: -4,
    right: -4,
    minWidth: 20,
    height: 20,
    paddingHorizontal: 5,
    borderRadius: 10,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  badgeText: { color: '#fff', fontSize: 11, lineHeight: 13, fontWeight: 700 },
});
