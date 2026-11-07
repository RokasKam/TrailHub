import { router } from 'expo-router';

import { useGearLists } from '@/api/gear';
import { ThemedText } from '@/components/themed-text';
import { Button, List, ListRow, QueryState, Screen, Section } from '@/components/ui';

/** Your reusable packing lists, plus the starter lists to copy from. */
export default function GearListsScreen() {
  const lists = useGearLists();
  const mine = lists.data?.filter((l) => !l.isStarter) ?? [];
  const starters = lists.data?.filter((l) => l.isStarter) ?? [];

  return (
    <Screen>
      <ThemedText type="small" themeColor="textSecondary">
        Make a list once, then add it to any trip&apos;s checklist with one tap.
      </ThemedText>
      <QueryState isLoading={lists.isLoading} error={lists.error} hasData={!!lists.data?.length} onRetry={() => lists.refetch()} />

      <Section title="Your lists" action={<Button size="small" variant="tinted" icon="plus" label="New" onPress={() => router.push('/gear/new')} />}>
        {mine.length === 0 ? (
          <ThemedText type="small" themeColor="textSecondary">
            No lists yet. Start from a starter list below, or save a trip&apos;s checklist as a list.
          </ThemedText>
        ) : (
          <List>
            {mine.map((l) => (
              <ListRow key={l.id} icon="checklist" iconColor="#1F7A4D" title={l.name} detail={`${l.items.length} item${l.items.length === 1 ? '' : 's'}`} onPress={() => router.push(`/gear/${l.id}`)} />
            ))}
          </List>
        )}
      </Section>

      {starters.length > 0 && (
        <Section title="Starter lists">
          <List>
            {starters.map((l) => (
              <ListRow key={l.id} icon="sparkles" iconColor="#7C3AED" title={l.name} detail={`${l.items.length} items`} onPress={() => router.push(`/gear/${encodeURIComponent(l.id)}`)} />
            ))}
          </List>
        </Section>
      )}
    </Screen>
  );
}
