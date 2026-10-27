import { useEffect, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useUnits } from '@/api/profile';
import { useFavoriteTrails, useTrailSearch } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { Button, Card, Input, QueryState } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDistance, formatElevation } from '@/lib/format';
import type { TrailSummary } from '@/lib/types';

type TrailPickerProps = {
  visible: boolean;
  title?: string;
  onSelect: (trail: TrailSummary) => void;
  onClose: () => void;
};

/** Full-screen trail search: favorites first, then any trail by name or region. */
export function TrailPicker({ visible, title = 'Choose a trail', onSelect, onClose }: TrailPickerProps) {
  const insets = useSafeAreaInsets();
  const units = useUnits();
  const [query, setQuery] = useState('');
  const [debounced, setDebounced] = useState('');
  useEffect(() => {
    const t = setTimeout(() => setDebounced(query.trim()), 300);
    return () => clearTimeout(t);
  }, [query]);

  const favorites = useFavoriteTrails();
  const search = useTrailSearch({ q: debounced || undefined, sort: debounced ? 'Relevance' : 'Rating' });
  const favoriteIds = new Set(favorites.data?.map((f) => f.id));
  const results = [
    ...(debounced ? [] : (favorites.data ?? [])),
    ...(search.data?.items ?? []).filter((t) => debounced || !favoriteIds.has(t.id)),
  ];

  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
      <ThemedView style={[styles.container, { paddingBottom: insets.bottom + Spacing.three }]}>
        <View style={styles.header}>
          <ThemedText type="subtitle" style={styles.title}>
            {title}
          </ThemedText>
          <Button label="Close" variant="secondary" onPress={onClose} />
        </View>
        <Input placeholder="Search trails or regions" value={query} onChangeText={setQuery} autoFocus returnKeyType="search" />
        <ScrollView contentContainerStyle={styles.list} keyboardShouldPersistTaps="handled">
          <QueryState isLoading={search.isLoading} error={search.error} empty={!search.isLoading && results.length === 0} emptyText="No trails found." />
          {results.map((t) => (
            <Pressable key={t.id} onPress={() => onSelect(t)} style={({ pressed }) => pressed && { opacity: 0.7 }}>
              <Card>
                <ThemedText type="smallBold">
                  {favoriteIds.has(t.id) ? '★ ' : ''}
                  {t.name}
                </ThemedText>
                <ThemedText type="small" themeColor="textSecondary">
                  {t.region} · {formatDistance(t.distanceMeters, units)} · ↑ {formatElevation(t.elevationGainMeters, units)} · {t.difficulty}
                </ThemedText>
              </Card>
            </Pressable>
          ))}
        </ScrollView>
      </ThemedView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: Spacing.three, gap: Spacing.three },
  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  title: { flex: 1, fontSize: 24, lineHeight: 32 },
  list: { gap: Spacing.two, paddingBottom: Spacing.four },
});
