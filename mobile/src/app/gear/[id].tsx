import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useDeleteGearList, useGearLists, useSaveGearList } from '@/api/gear';
import { ThemedText } from '@/components/themed-text';
import { Badge, Button, Card, Icon, Input, List, ListRow, QueryState, Screen, Section, Segmented } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import type { ChecklistItemKind, GearItem } from '@/lib/types';

/** Edit one of your gear lists; a starter list opens read-only with "Save a copy". */
export default function GearListScreen() {
  const { id: raw } = useLocalSearchParams<{ id: string }>();
  const id = decodeURIComponent(raw);
  const isNew = id === 'new';
  const lists = useGearLists();
  const existing = lists.data?.find((l) => l.id === id);

  if (!isNew && !existing) return <Screen><QueryState isLoading={lists.isLoading} error={lists.error} empty={!lists.isLoading} emptyText="This list doesn't exist." emptyIcon="checklist" /></Screen>;
  return <Editor key={id} listId={isNew ? null : id} starter={existing?.isStarter ?? false} initialName={existing?.name ?? ''} initialItems={existing?.items ?? []} />;
}

function Editor({ listId, starter, initialName, initialItems }: { listId: string | null; starter: boolean; initialName: string; initialItems: GearItem[] }) {
  const theme = useTheme();
  const save = useSaveGearList();
  const remove = useDeleteGearList();
  const [name, setName] = useState(starter ? `My ${initialName.toLowerCase()}` : initialName);
  const [items, setItems] = useState<GearItem[]>(initialItems);
  const [text, setText] = useState('');
  const [kind, setKind] = useState<ChecklistItemKind>('Personal');

  const add = () => {
    const t = text.trim();
    if (!t) return;
    if (items.some((i) => i.text.toLowerCase() === t.toLowerCase())) return Alert.alert('Already on the list', t);
    setItems([...items, { text: t, kind }]);
    setText('');
  };
  const submit = () =>
    save.mutate(
      { id: starter ? null : listId, name: name.trim(), items },
      { onSuccess: (l) => (listId && !starter ? router.back() : router.replace(`/gear/${l.id}`)), onError: (e) => Alert.alert("Couldn't save", e.message) },
    );

  return (
    <Screen>
      <Stack.Screen options={{ title: starter ? 'Starter list' : listId ? 'Gear list' : 'New gear list' }} />
      {starter && (
        <Card flat style={styles.row}>
          <Icon name="sparkles" size={14} color="#7C3AED" />
          <ThemedText type="small" style={{ flex: 1 }}>
            A starter list. Change it as you like, then save it as your own.
          </ThemedText>
        </Card>
      )}
      <Input icon="pencil" placeholder="List name, e.g. Hut trip kit" value={name} onChangeText={setName} />

      <Section title={`Items · ${items.length}`}>
        {items.length > 0 && (
          <List>
            {items.map((item, i) => (
              <ListRow
                key={item.text}
                icon={item.kind === 'Shared' ? 'person.3.fill' : 'person.fill'}
                iconColor={item.kind === 'Shared' ? '#2563EB' : '#64748B'}
                title={item.text}
                trailing={
                  <View style={styles.row}>
                    <Pressable
                      accessibilityLabel={item.kind === 'Shared' ? 'Make it personal' : 'Make it group gear'}
                      onPress={() => setItems(items.map((x, j) => (j === i ? { ...x, kind: x.kind === 'Shared' ? 'Personal' : 'Shared' } : x)))}>
                      <Badge label={item.kind === 'Shared' ? 'Group' : 'Personal'} color={item.kind === 'Shared' ? '#2563EB' : theme.textSecondary} />
                    </Pressable>
                    <Pressable accessibilityLabel={`Remove ${item.text}`} hitSlop={8} onPress={() => setItems(items.filter((_, j) => j !== i))}>
                      <Icon name="minus.circle.fill" size={20} color={theme.danger} />
                    </Pressable>
                  </View>
                }
              />
            ))}
          </List>
        )}
        <Segmented
          value={kind}
          onChange={setKind}
          options={[
            { value: 'Personal', label: 'Everyone brings', icon: 'person.2' },
            { value: 'Shared', label: 'Group gear', icon: 'tent' },
          ]}
        />
        <Input icon="plus.circle" placeholder="Add item" value={text} onChangeText={setText} returnKeyType="done" onSubmitEditing={add} blurOnSubmit={false} />
        <ThemedText type="small" themeColor="textSecondary">
          Group gear is something one person brings for everyone (stove, tent). On trips you don&apos;t share with a group, it&apos;s added as a personal item.
        </ThemedText>
      </Section>

      <Button icon="square.and.arrow.down" label={starter ? 'Save as my list' : 'Save'} onPress={submit} loading={save.isPending} disabled={!name.trim() || items.length === 0} />
      {listId && !starter && (
        <List>
          <ListRow
            title="Delete list"
            destructive
            onPress={() =>
              Alert.alert('Delete this list?', 'Trips you added it to keep their items.', [
                { text: 'Cancel', style: 'cancel' },
                { text: 'Delete', style: 'destructive', onPress: () => remove.mutate(listId, { onSuccess: () => router.back() }) },
              ])
            }
          />
        </List>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
});
