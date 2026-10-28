import { useState } from 'react';
import { ActionSheetIOS, Alert, Platform, Pressable, StyleSheet, View } from 'react-native';

import { useTripChange } from '@/api/planning';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Icon, Input, List, Section, Segmented } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import type { ChecklistItem, ChecklistItemKind, Trip } from '@/lib/types';

type Change = ReturnType<typeof useTripChange>;

/** Shows a list of actions as an iOS action sheet, or an alert elsewhere. */
function chooseAction(title: string, actions: { label: string; destructive?: boolean; run: () => void }[]) {
  if (actions.length === 0) return;
  if (Platform.OS === 'ios') {
    ActionSheetIOS.showActionSheetWithOptions(
      {
        title,
        options: [...actions.map((a) => a.label), 'Cancel'],
        cancelButtonIndex: actions.length,
        destructiveButtonIndex: actions.flatMap((a, i) => (a.destructive ? [i] : [])),
      },
      (i) => actions[i]?.run(),
    );
  } else {
    Alert.alert(title, undefined, [...actions.map((a) => ({ text: a.label, style: a.destructive ? ('destructive' as const) : undefined, onPress: a.run })), { text: 'Cancel', style: 'cancel' }]);
  }
}

/**
 * The trip's packing list. On a private trip it's one simple list. On a trip shared with a group it splits into
 * group gear (one person brings it: "I'll bring it") and personal items (everyone ticks their own), plus
 * everyone's packing progress.
 */
export function TripChecklist({ trip, myId, change }: { trip: Trip; myId?: string; change: Change }) {
  const shared = trip.groupId != null;
  const gear = trip.checklist.filter((c) => c.kind === 'Shared');
  const personal = trip.checklist.filter((c) => c.kind === 'Personal');
  const fail = (title: string) => (e: Error) => Alert.alert(title, e.message);

  const itemActions = (c: ChecklistItem) => {
    const actions: { label: string; destructive?: boolean; run: () => void }[] = [];
    if (c.kind === 'Shared' && c.claimedBy === myId) {
      actions.push({ label: "I can't bring it", run: () => change.mutate({ kind: 'claimItem', itemId: c.id, claim: false }, { onError: fail("Couldn't update") }) });
    } else if (c.kind === 'Shared' && c.claimedBy && trip.isOwner) {
      actions.push({ label: `Release ${c.claimedByName ?? 'claim'}`, run: () => change.mutate({ kind: 'claimItem', itemId: c.id, claim: false }, { onError: fail("Couldn't update") }) });
    }
    if (c.canRemove) {
      actions.push({ label: 'Remove item', destructive: true, run: () => change.mutate({ kind: 'removeItem', itemId: c.id }, { onError: fail("Couldn't remove") }) });
    }
    chooseAction(c.text, actions);
  };

  if (!shared) {
    return (
      <Section title="Checklist">
        {personal.length + gear.length > 0 && (
          <List>
            {[...gear, ...personal].map((c) => (
              <CheckRow
                key={c.id}
                item={c}
                onToggle={() => change.mutate({ kind: 'toggleItem', itemId: c.id, isDone: !c.isDone })}
                onLongPress={() => itemActions(c)}
              />
            ))}
          </List>
        )}
        <AddItem change={change} shared={false} />
      </Section>
    );
  }

  const unclaimed = gear.filter((c) => !c.claimedBy).length;

  return (
    <>
      <Section
        title="Group gear"
        action={
          gear.length > 0 ? (
            <ThemedText type="small" themeColor="textSecondary">
              {unclaimed === 0 ? 'All covered' : `${unclaimed} unclaimed`}
            </ThemedText>
          ) : undefined
        }>
        {gear.length === 0 ? (
          <ThemedText type="small" themeColor="textSecondary">
            Things only one person needs to bring, like a stove or a tent. Add them below and people can claim them.
          </ThemedText>
        ) : (
          <List>
            {gear.map((c) => (
              <GearRow
                key={c.id}
                item={c}
                mine={c.claimedBy === myId}
                onClaim={() => change.mutate({ kind: 'claimItem', itemId: c.id, claim: true }, { onError: fail("Couldn't claim it") })}
                onToggle={() => change.mutate({ kind: 'toggleItem', itemId: c.id, isDone: !c.isDone }, { onError: fail("Couldn't update") })}
                onLongPress={() => itemActions(c)}
              />
            ))}
          </List>
        )}
      </Section>

      <Section title="Everyone brings">
        {personal.length === 0 ? (
          <ThemedText type="small" themeColor="textSecondary">
            Things each person packs for themselves, like a headlamp or water. Everyone ticks their own.
          </ThemedText>
        ) : (
          <List>
            {personal.map((c) => (
              <CheckRow
                key={c.id}
                item={c}
                onToggle={() => change.mutate({ kind: 'toggleItem', itemId: c.id, isDone: !c.isDone }, { onError: fail("Couldn't update") })}
                onLongPress={() => itemActions(c)}
              />
            ))}
          </List>
        )}
        <AddItem change={change} shared />
      </Section>

      {personal.length > 0 && trip.members.length > 0 && <PackingProgress trip={trip} total={personal.length} myId={myId} />}
    </>
  );
}

function CheckRow({ item, onToggle, onLongPress }: { item: ChecklistItem; onToggle: () => void; onLongPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable onPress={onToggle} onLongPress={onLongPress} accessibilityRole="checkbox" accessibilityState={{ checked: item.isDone }}>
      <View style={styles.row}>
        <Icon name={item.isDone ? 'checkmark.circle.fill' : 'circle'} size={22} color={item.isDone ? theme.accent : theme.textSecondary} weight="regular" />
        <ThemedText style={[{ flex: 1 }, item.isDone && styles.done]}>{item.text}</ThemedText>
      </View>
    </Pressable>
  );
}

/** Group gear: unclaimed shows "I'll bring it"; mine can be ticked when packed; someone else's shows their name. */
function GearRow({ item, mine, onClaim, onToggle, onLongPress }: { item: ChecklistItem; mine: boolean; onClaim: () => void; onToggle: () => void; onLongPress: () => void }) {
  const theme = useTheme();
  const claimed = item.claimedBy != null;
  const detail = !claimed ? 'Nobody yet' : mine ? (item.isDone ? 'You packed it' : "You're bringing it") : `${item.claimedByName} ${item.isDone ? 'packed it' : 'is bringing it'}`;

  return (
    <Pressable onPress={mine ? onToggle : undefined} onLongPress={onLongPress} accessibilityRole={mine ? 'checkbox' : undefined} accessibilityState={mine ? { checked: item.isDone } : undefined}>
      <View style={styles.row}>
        {claimed ? (
          <Icon
            name={item.isDone ? 'checkmark.circle.fill' : mine ? 'circle' : 'person.crop.circle.fill'}
            size={22}
            color={item.isDone ? theme.accent : mine ? theme.textSecondary : '#2563EB'}
            weight="regular"
          />
        ) : (
          <Icon name="questionmark.circle" size={22} color="#D97706" weight="regular" />
        )}
        <View style={{ flex: 1 }}>
          <ThemedText style={item.isDone && styles.done}>{item.text}</ThemedText>
          <ThemedText type="small" themeColor="textSecondary" style={!claimed && { color: '#D97706' }}>
            {detail}
          </ThemedText>
        </View>
        {!claimed && <Button size="small" variant="tinted" icon="hand.raised.fill" label="I'll bring it" onPress={onClaim} />}
      </View>
    </Pressable>
  );
}

function AddItem({ change, shared }: { change: Change; shared: boolean }) {
  const [text, setText] = useState('');
  const [kind, setKind] = useState<ChecklistItemKind>('Personal');
  const submit = () =>
    text.trim() &&
    change.mutate(
      { kind: 'addItem', text, itemKind: shared ? kind : 'Personal' },
      { onSuccess: () => setText(''), onError: (e) => Alert.alert("Couldn't add item", e.message) },
    );

  return (
    <View style={{ gap: Spacing.two }}>
      {shared && (
        <Segmented
          value={kind}
          onChange={setKind}
          options={[
            { value: 'Personal', label: 'Everyone brings', icon: 'person.2' },
            { value: 'Shared', label: 'Group gear', icon: 'tent' },
          ]}
        />
      )}
      <Input
        icon="plus.circle"
        placeholder={shared && kind === 'Shared' ? 'Add group gear, e.g. Stove' : 'Add item (long-press an item for options)'}
        value={text}
        onChangeText={setText}
        returnKeyType="done"
        onSubmitEditing={submit}
      />
    </View>
  );
}

function PackingProgress({ trip, total, myId }: { trip: Trip; total: number; myId?: string }) {
  const theme = useTheme();
  return (
    <Section title="Packing progress">
      <Card>
        {trip.members.map((m) => {
          const ratio = Math.min(1, m.personalDone / total);
          return (
            <View key={m.userId} style={styles.member}>
              <View style={styles.memberHeader}>
                <ThemedText type="smallBold" style={{ flex: 1 }} numberOfLines={1}>
                  {m.userId === myId ? 'You' : m.displayName}
                  {m.isOwner && <ThemedText type="small" themeColor="textSecondary"> · planner</ThemedText>}
                </ThemedText>
                <ThemedText type="small" themeColor="textSecondary" style={{ fontVariant: ['tabular-nums'] }}>
                  {m.personalDone}/{total}
                </ThemedText>
              </View>
              <View style={[styles.track, { backgroundColor: theme.backgroundSelected }]}>
                <View style={[styles.fill, { width: `${ratio * 100}%`, backgroundColor: ratio === 1 ? theme.accent : '#2563EB' }]} />
              </View>
            </View>
          );
        })}
      </Card>
    </Section>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4, minHeight: 48, paddingVertical: Spacing.one },
  done: { textDecorationLine: 'line-through', opacity: 0.5 },
  member: { gap: 6 },
  memberHeader: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  track: { height: 6, borderRadius: Radius.pill, overflow: 'hidden' },
  fill: { height: '100%', borderRadius: Radius.pill },
});
