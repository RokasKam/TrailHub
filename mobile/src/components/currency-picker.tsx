import { useState } from 'react';
import { Modal, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useCurrencies } from '@/api/expenses';
import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { Icon, IconButton, Input, List, ListRow, QueryState } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import type { Currency } from '@/lib/types';

type CurrencyPickerProps = {
  visible: boolean;
  title?: string;
  selected: string;
  /** Listed first, e.g. the trip's currency when entering an expense. */
  pinned?: string;
  onSelect: (code: string) => void;
  onClose: () => void;
};

/** Searchable list of the currencies the exchange-rate provider knows. */
export function CurrencyPicker({ visible, title = 'Currency', selected, pinned, onSelect, onClose }: CurrencyPickerProps) {
  const insets = useSafeAreaInsets();
  const theme = useTheme();
  const currencies = useCurrencies();
  const [query, setQuery] = useState('');

  const q = query.trim().toLowerCase();
  const all: Currency[] = currencies.data ?? [];
  // The pinned/selected codes may be missing from an offline fallback list; keep them pickable.
  const known = new Set(all.map((c) => c.code));
  const extra = [pinned, selected].filter((c): c is string => !!c && !known.has(c)).map((code) => ({ code, name: code }));
  const list = [...extra, ...all]
    .filter((c) => !q || c.code.toLowerCase().includes(q) || c.name.toLowerCase().includes(q))
    .sort((a, b) => Number(b.code === pinned) - Number(a.code === pinned) || a.code.localeCompare(b.code));

  const close = () => {
    setQuery('');
    onClose();
  };

  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={close}>
      <ThemedView type="background" style={[styles.container, { paddingBottom: insets.bottom + Spacing.three }]}>
        <View style={styles.header}>
          <ThemedText type="subtitle" style={styles.title}>
            {title}
          </ThemedText>
          <IconButton icon="xmark" label="Close" onPress={close} />
        </View>
        <Input icon="magnifyingglass" placeholder="Search, e.g. CHF or Swiss" value={query} onChangeText={setQuery} autoCapitalize="none" autoCorrect={false} returnKeyType="search" />
        <ScrollView contentContainerStyle={styles.list} keyboardShouldPersistTaps="handled">
          <QueryState isLoading={currencies.isLoading} error={currencies.error} hasData={!!currencies.data} onRetry={() => void currencies.refetch()} />
          {list.length === 0 && !currencies.isLoading && !!currencies.data && (
            <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
              No currency matches “{query.trim()}”.
            </ThemedText>
          )}
          {list.length > 0 && (
            <List>
              {list.map((c) => (
                <ListRow
                  key={c.code}
                  title={`${c.code} · ${c.name}`}
                  detail={c.code === pinned ? 'Trip currency' : undefined}
                  trailing={c.code === selected ? <Icon name="checkmark" size={16} color={theme.accent} /> : <View />}
                  onPress={() => {
                    setQuery('');
                    onSelect(c.code);
                  }}
                />
              ))}
            </List>
          )}
        </ScrollView>
      </ThemedView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: Spacing.three + 2, paddingTop: Spacing.four, gap: Spacing.three },
  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  title: { flex: 1 },
  list: { gap: Spacing.two, paddingBottom: Spacing.four },
});
