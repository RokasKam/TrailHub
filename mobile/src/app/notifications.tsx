import { type Href, router, useFocusEffect } from 'expo-router';
import { useCallback, useEffect } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { useMarkAllRead, useMarkRead, useNotifications } from '@/api/notifications';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Icon, List, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatTimeAgo } from '@/lib/format';
import type { AppIcon } from '@/lib/icons';
import type { AppNotification } from '@/lib/types';
import { promptForPushOnce, refreshPushPermission, requestPushPermission, usePushPermission } from '@/notifications/push';

/** Category icon; the kind (set by the backend) tells likes from comments and expenses from gear reminders. */
function iconFor(n: AppNotification): { icon: AppIcon; color: string } {
  switch (n.category) {
    case 'LikesComments':
      return /comment/i.test(n.kind) ? { icon: 'bubble.left.fill', color: '#0891B2' } : { icon: 'heart.fill', color: '#E11D48' };
    case 'Group':
      return { icon: 'person.3.fill', color: '#2563EB' };
    case 'Trips':
      return /gear/i.test(n.kind) ? { icon: 'backpack.fill', color: '#EA580C' } : { icon: 'banknote.fill', color: '#EA580C' };
    default:
      return { icon: 'bell.fill', color: '#1F7A4D' };
  }
}

function isToday(iso: string) {
  return new Date(iso).toDateString() === new Date().toDateString();
}

export default function NotificationsScreen() {
  const list = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const permission = usePushPermission((s) => s.status);
  const { refetch } = list;

  // First visit asks for push permission (with an explanation); later visits only refresh the card's state.
  useEffect(() => {
    void refreshPushPermission().then(() => promptForPushOnce());
  }, []);

  useFocusEffect(
    useCallback(() => {
      void refetch();
    }, [refetch]),
  );

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const today = items.filter((n) => isToday(n.updatedAtUtc));
  const earlier = items.filter((n) => !isToday(n.updatedAtUtc));
  const hasUnread = items.some((n) => !n.read);

  const open = (n: AppNotification) => {
    if (!n.read) markRead.mutate(n.id);
    if (n.targetUrl?.startsWith('/')) router.push(n.targetUrl as Href);
  };

  const markAllAction = hasUnread ? <Button label="Mark all read" icon="checkmark" variant="tinted" size="small" onPress={() => markAll.mutate()} /> : undefined;

  return (
    <Screen
      refreshing={list.isRefetching && !list.isFetchingNextPage}
      onRefresh={() => void refetch()}
      onEndReached={() => list.hasNextPage && !list.isFetchingNextPage && void list.fetchNextPage()}>
      {(permission === 'undetermined' || permission === 'blocked') && <TurnOnCard blocked={permission === 'blocked'} />}

      <QueryState
        isLoading={list.isLoading}
        error={list.error}
        hasData={items.length > 0}
        onRetry={() => void refetch()}
        empty={list.isSuccess && items.length === 0}
        emptyIcon="bell"
        emptyText="No notifications yet. Likes, comments, group hikes and trip expenses show up here."
      />

      {today.length > 0 && (
        <Section title="Today" action={markAllAction}>
          <NotificationList items={today} onOpen={open} />
        </Section>
      )}
      {earlier.length > 0 && (
        <Section title="Earlier" action={today.length ? undefined : markAllAction}>
          <NotificationList items={earlier} onOpen={open} />
        </Section>
      )}
    </Screen>
  );
}

function TurnOnCard({ blocked }: { blocked: boolean }) {
  const theme = useTheme();
  return (
    <Card>
      <View style={styles.cardHeader}>
        <View style={[styles.icon, { backgroundColor: theme.accentSoft }]}>
          <Icon name="bell.slash" size={16} color={theme.accent} />
        </View>
        <View style={{ flex: 1 }}>
          <ThemedText type="smallBold">Turn on notifications</ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            Hear about likes, comments, group hikes and trip expenses even when TrailHub is closed.
          </ThemedText>
        </View>
      </View>
      <Button label={blocked ? 'Open Settings' : 'Turn on'} icon="bell.fill" variant="tinted" size="small" onPress={() => void requestPushPermission()} />
    </Card>
  );
}

function NotificationList({ items, onOpen }: { items: AppNotification[]; onOpen: (n: AppNotification) => void }) {
  return (
    <List>
      {items.map((n) => (
        <NotificationRow key={n.id} item={n} onPress={() => onOpen(n)} />
      ))}
    </List>
  );
}

function NotificationRow({ item, onPress }: { item: AppNotification; onPress: () => void }) {
  const theme = useTheme();
  const { icon, color } = iconFor(item);
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${item.read ? '' : 'Unread. '}${item.title}. ${item.body}`}
      style={({ pressed }) => [styles.row, pressed && { opacity: 0.6 }]}>
      <View style={[styles.icon, { backgroundColor: color }]}>
        <Icon name={icon} size={15} color="#fff" />
      </View>
      <View style={styles.text}>
        <View style={styles.titleLine}>
          <ThemedText type={item.read ? 'small' : 'smallBold'} style={{ flex: 1 }} numberOfLines={2}>
            {item.title}
          </ThemedText>
          <ThemedText type="caption" themeColor="textSecondary">
            {formatTimeAgo(item.updatedAtUtc)}
          </ThemedText>
        </View>
        {!!item.body && (
          <ThemedText type="small" themeColor="textSecondary" numberOfLines={3}>
            {item.body}
          </ThemedText>
        )}
      </View>
      <View style={[styles.dot, { backgroundColor: item.read ? 'transparent' : theme.accent }]} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  cardHeader: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.three - 4 },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.three - 4, minHeight: 56, paddingVertical: Spacing.two + 2 },
  icon: { width: 30, height: 30, borderRadius: 8, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center' },
  text: { flex: 1, gap: 2 },
  titleLine: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.two },
  dot: { width: 8, height: 8, borderRadius: 4, marginTop: 6 },
});
