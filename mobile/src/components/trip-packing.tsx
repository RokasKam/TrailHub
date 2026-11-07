import { useState } from 'react';
import { Alert, StyleSheet, View } from 'react-native';

import { gearFactsFrom, useGearLists, useGearSuggestions, useTripGear } from '@/api/gear';
import { ThemedText } from '@/components/themed-text';
import { Badge, Button, Card, Chip, Icon, Input, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import type { PlannedRoute, Trip } from '@/lib/types';

const SHOWN_SUGGESTIONS = 6;

/**
 * Helps fill the trip's checklist: add one of your gear lists, pick from suggestions based on the trip (nights,
 * where you sleep, altitude, longest day), or save what's here as a list for next time.
 */
export function TripPacking({ trip, route }: { trip: Trip; route?: PlannedRoute }) {
  const theme = useTheme();
  const lists = useGearLists();
  const gear = useTripGear(trip.id);
  const suggestions = useGearSuggestions(trip.id, gearFactsFrom(route), trip.checklist.length);
  const [showAll, setShowAll] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const fail = (title: string) => (e: Error) => Alert.alert(title, e.message);

  const shown = showAll ? (suggestions.data ?? []) : (suggestions.data ?? []).slice(0, SHOWN_SUGGESTIONS);
  const hidden = (suggestions.data?.length ?? 0) - shown.length;

  return (
    <Section title="Pack faster">
      {!!lists.data?.length && (
        <Card>
          <ThemedText type="smallBold">Add a gear list</ThemedText>
          <View style={styles.chips}>
            {lists.data.map((l) => (
              <Chip
                key={l.id}
                icon={l.isStarter ? 'sparkles' : 'checklist'}
                label={l.name}
                onPress={() =>
                  gear.addList.mutate(l.id, {
                    onSuccess: (t) => {
                      const added = t.checklist.length - trip.checklist.length;
                      Alert.alert(added > 0 ? `Added ${added} item${added === 1 ? '' : 's'}` : 'Nothing new', added > 0 ? `From “${l.name}”.` : 'Everything on that list is already here.');
                    },
                    onError: fail("Couldn't add the list"),
                  })
                }
              />
            ))}
          </View>
          <ThemedText type="small" themeColor="textSecondary">
            Items already on the checklist are skipped.
          </ThemedText>
        </Card>
      )}

      {!!suggestions.data?.length && (
        <Card>
          <View style={styles.row}>
            <Icon name="lightbulb.fill" size={14} color="#D97706" />
            <ThemedText type="smallBold" style={{ flex: 1 }}>
              Suggested for this trip
            </ThemedText>
            <Button size="small" variant="tinted" label="Add all" loading={gear.addItems.isPending} onPress={() => gear.addItems.mutate(suggestions.data!, { onError: fail("Couldn't add") })} />
          </View>
          {shown.map((s) => (
            <View key={s.text} style={styles.row}>
              <View style={{ flex: 1 }}>
                <ThemedText type="small">{s.text}</ThemedText>
                <ThemedText type="caption" themeColor="textSecondary">
                  {s.reason}
                </ThemedText>
              </View>
              {s.kind === 'Shared' && <Badge label="Group" color="#2563EB" />}
              <Button size="small" variant="secondary" icon="plus" label="Add" onPress={() => gear.addItems.mutate([{ text: s.text, kind: s.kind }], { onError: fail("Couldn't add") })} />
            </View>
          ))}
          {hidden > 0 && <Button size="small" variant="secondary" label={`Show ${hidden} more`} onPress={() => setShowAll(true)} />}
        </Card>
      )}
      {suggestions.data?.length === 0 && trip.checklist.length > 0 && (
        <View style={styles.row}>
          <Icon name="checkmark.seal.fill" size={14} color={theme.accent} />
          <ThemedText type="small" themeColor="textSecondary">
            Your checklist covers everything we&apos;d suggest for this trip.
          </ThemedText>
        </View>
      )}

      {trip.checklist.length > 0 &&
        (saving == null ? (
          <Button size="small" variant="secondary" icon="square.and.arrow.down" label="Save checklist as a gear list" onPress={() => setSaving(trip.name)} />
        ) : (
          <Card>
            <Input icon="pencil" placeholder="List name" value={saving} onChangeText={setSaving} autoFocus />
            <View style={styles.row}>
              <View style={{ flex: 1 }}>
                <Button size="small" variant="secondary" label="Cancel" onPress={() => setSaving(null)} />
              </View>
              <View style={{ flex: 1 }}>
                <Button
                  size="small"
                  label="Save"
                  loading={gear.saveAsList.isPending}
                  disabled={!saving.trim()}
                  onPress={() =>
                    gear.saveAsList.mutate(saving.trim(), {
                      onSuccess: (l) => {
                        setSaving(null);
                        Alert.alert('Saved', `“${l.name}” is in Profile → Gear lists.`);
                      },
                      onError: fail("Couldn't save"),
                    })
                  }
                />
              </View>
            </View>
          </Card>
        ))}
    </Section>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
