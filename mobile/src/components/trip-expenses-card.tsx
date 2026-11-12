import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useSetTripCurrency } from '@/api/expenses';
import { CurrencyPicker } from '@/components/currency-picker';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Icon, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatMoney, netColor, toCents } from '@/lib/money';
import type { Trip } from '@/lib/types';

/** "You're owed €12.40" / "You owe €8.00" / "All settled". */
export function balanceLine(myNet: number, currency: string) {
  const cents = toCents(myNet);
  if (cents > 0) return `You're owed ${formatMoney(myNet, currency)}`;
  if (cents < 0) return `You owe ${formatMoney(-myNet, currency)}`;
  return 'All settled';
}

/** The trip page's summary of shared costs, with the way into the expenses screen. */
export function TripExpensesCard({ trip }: { trip: Trip }) {
  const theme = useTheme();
  const setCurrency = useSetTripCurrency(trip.id);
  const [picking, setPicking] = useState(false);
  // Older cached trips (before expenses existed) have no summary yet; treat them as empty.
  const summary = trip.expenseSummary ?? { total: 0, myNet: 0, expenseCount: 0 };
  const currency = trip.currency ?? 'EUR';
  const empty = summary.expenseCount === 0;
  const canChangeCurrency = trip.isOwner && empty;
  const open = () => router.push(`/trips/${trip.id}/expenses`);

  return (
    <Section title="Expenses">
      <Card>
        <Pressable onPress={open} accessibilityRole="button" accessibilityLabel="Open expenses" style={({ pressed }) => [styles.top, pressed && { opacity: 0.6 }]}>
          <View style={[styles.icon, { backgroundColor: theme.accentSoft }]}>
            <Icon name="banknote.fill" size={18} color={theme.accent} />
          </View>
          <View style={{ flex: 1 }}>
            <ThemedText type="caption" themeColor="textSecondary">
              {empty ? 'NOTHING LOGGED YET' : `SPENT · ${summary.expenseCount} EXPENSE${summary.expenseCount === 1 ? '' : 'S'}`}
            </ThemedText>
            <ThemedText type="number">{formatMoney(summary.total, currency)}</ThemedText>
          </View>
          {!empty && (
            <ThemedText type="smallBold" style={[styles.balance, { color: netColor(summary.myNet, theme) }]} numberOfLines={2}>
              {balanceLine(summary.myNet, currency)}
            </ThemedText>
          )}
        </Pressable>
        {empty && (
          <ThemedText type="small" themeColor="textSecondary">
            {trip.groupId
              ? 'Log what people pay for (huts, food, fuel) and split it. TrailHub works out who owes whom.'
              : 'Keep track of what this trip costs. Share it with a group to split costs.'}
          </ThemedText>
        )}
        {canChangeCurrency && (
          <Pressable onPress={() => setPicking(true)} accessibilityRole="button" style={({ pressed }) => [styles.currency, pressed && { opacity: 0.6 }]}>
            <Icon name="dollarsign.circle" size={14} color={theme.textSecondary} />
            <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
              Trip currency: <ThemedText type="smallBold">{currency}</ThemedText>
            </ThemedText>
            <ThemedText type="smallBold" style={{ color: theme.accent }}>
              {setCurrency.isPending ? 'Saving…' : 'Change'}
            </ThemedText>
          </Pressable>
        )}
        <Button icon={empty ? 'plus' : 'list.bullet'} variant="tinted" label={empty ? 'Add an expense' : 'View expenses & balances'} onPress={open} />
      </Card>
      {canChangeCurrency && (
        <CurrencyPicker
          visible={picking}
          title="Trip currency"
          selected={currency}
          onClose={() => setPicking(false)}
          onSelect={(code) => {
            setPicking(false);
            if (code !== currency) setCurrency.mutate(code, { onError: (e) => Alert.alert("Couldn't change the currency", e.message) });
          }}
        />
      )}
    </Section>
  );
}

const styles = StyleSheet.create({
  top: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4 },
  icon: { width: 40, height: 40, borderRadius: 12, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center' },
  balance: { textAlign: 'right', maxWidth: 150, fontVariant: ['tabular-nums'] },
  currency: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
});
