import { router } from 'expo-router';
import { Alert, StyleSheet, View } from 'react-native';

import { useModerateTrail, useModerationQueue } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { Badge, Button, Card, QueryState, Screen } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import type { ModerationItem } from '@/lib/types';

const REASON_LABELS: Record<string, string> = { Inaccurate: 'Inaccurate', Dangerous: 'Dangerous', Inappropriate: 'Inappropriate', Spam: 'Spam', Other: 'Other' };

/** Moderators: trails people reported, and trails taken out of Discover, with hide / restore. */
export default function ModerationScreen() {
  const queue = useModerationQueue();
  return (
    <Screen>
      <QueryState
        isLoading={queue.isLoading}
        error={queue.error}
        hasData={!!queue.data?.length}
        empty={queue.data?.length === 0}
        emptyText="Nothing to review. Reported trails show up here."
        emptyIcon="checkmark.shield"
        onRetry={() => queue.refetch()}
      />
      {queue.data?.map((item) => <ModerationCard key={item.trailId} item={item} />)}
    </Screen>
  );
}

function ModerationCard({ item }: { item: ModerationItem }) {
  const moderate = useModerateTrail();
  const set = (hidden: boolean) => moderate.mutate({ id: item.trailId, hidden }, { onError: (e) => Alert.alert("Couldn't update", e.message) });
  return (
    <Card>
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <ThemedText type="headline" onPress={() => router.push(`/trail/${item.trailId}`)}>
            {item.name}
          </ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            {item.region}
          </ThemedText>
        </View>
        {item.hiddenBy ? <Badge label={item.hiddenBy === 'Reports' ? 'Auto-hidden' : 'Hidden'} color="#B45309" icon="eye.slash" /> : <Badge label="Live" color="#1F7A4D" />}
      </View>
      {item.reports.map((r, i) => (
        <ThemedText key={i} type="small">
          • {REASON_LABELS[r.reason] ?? r.reason}
          {r.details ? `: ${r.details}` : ''}
          <ThemedText type="small" themeColor="textSecondary">
            {'  '}
            {new Date(r.createdAtUtc).toLocaleDateString()}
          </ThemedText>
        </ThemedText>
      ))}
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="secondary" icon="eye" label={item.hiddenBy ? 'Restore' : 'Keep (dismiss)'} onPress={() => set(false)} loading={moderate.isPending} />
        </View>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="danger" icon="eye.slash" label="Hide" onPress={() => set(true)} disabled={item.hiddenBy === 'Moderator'} />
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
});
