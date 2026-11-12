import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, Platform, Pressable, StyleSheet, View } from 'react-native';

import { useDeleteExpense, useExchangeRate, useSaveExpense, useTripExpenses } from '@/api/expenses';
import { useSession } from '@/auth/session';
import { CurrencyPicker } from '@/components/currency-picker';
import { DateField, toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Chip, Icon, Input, List, ListRow, QueryState, Screen, Section, Segmented } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { confirmDelete } from '@/lib/action-sheet';
import { amountInputText, convert, EXPENSE_CATEGORIES, formatMoney, formatShortDate, fromCents, parseDecimal, toCents } from '@/lib/money';
import type { Expense, ExpenseCategory, ExpenseInput, SplitMode, TripExpenses } from '@/lib/types';

const MAX_AMOUNT = 1_000_000;

/** DateOnly strings are calendar dates; parse them as local midnight, not UTC. */
const fromDateOnly = (s: string) => {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
};

/** Modal: /trips/:id/expense adds an expense, /trips/:id/expense?expenseId=… edits one. */
export default function ExpenseScreen() {
  const { id, expenseId } = useLocalSearchParams<{ id: string; expenseId?: string }>();
  const theme = useTheme();
  const { user } = useSession();
  const ledger = useTripExpenses(id);

  const header = (
    <Stack.Screen
      options={{
        title: expenseId ? 'Edit expense' : 'New expense',
        // iOS modals have no back button; Android's header keeps its back arrow.
        headerRight:
          Platform.OS !== 'ios'
            ? undefined
            : () => (
                <Pressable accessibilityRole="button" accessibilityLabel="Close" hitSlop={12} onPress={() => router.back()}>
                  <Icon name="xmark" size={18} color={theme.text} />
                </Pressable>
              ),
      }}
    />
  );

  const data = ledger.data;
  const existing = expenseId ? data?.expenses.find((x) => x.id === expenseId) : undefined;
  if (!data || (expenseId && !existing)) {
    return (
      <Screen>
        {header}
        <QueryState
          isLoading={ledger.isLoading}
          error={ledger.error}
          empty={!!data}
          emptyIcon="banknote.fill"
          emptyText="This expense was deleted."
          onRetry={() => void ledger.refetch()}
        />
      </Screen>
    );
  }

  return (
    <Screen>
      {header}
      <Editor key={existing?.id ?? 'new'} tripId={id} data={data} existing={existing} myId={user?.id} />
    </Screen>
  );
}

/** The shares of a custom split, back in the expense's own currency (the API returns them in the trip currency). */
function customSharesInExpenseCurrency(x: Expense): Record<string, string> {
  const cents = x.shares.map((s) => ({ userId: s.userId, cents: x.rate === 1 ? toCents(s.amount) : Math.round((s.amount / x.rate) * 100) }));
  // Undoing the conversion can be a cent off; put the difference on the largest share so they sum to the amount.
  const diff = toCents(x.amount) - cents.reduce((sum, s) => sum + s.cents, 0);
  if (diff !== 0 && cents.length > 0) cents.reduce((a, b) => (b.cents > a.cents ? b : a)).cents += diff;
  return Object.fromEntries(cents.map((s) => [s.userId, amountInputText(fromCents(s.cents))]));
}

function Editor({ tripId, data, existing, myId }: { tripId: string; data: TripExpenses; existing?: Expense; myId?: string }) {
  const theme = useTheme();
  const save = useSaveExpense(tripId);
  const remove = useDeleteExpense(tripId);
  const people = data.participants;
  const tripCurrency = data.currency;
  const everyone = people.map((p) => p.userId);
  const defaultPayer = people.some((p) => p.userId === myId) ? myId : people[0]?.userId;

  const [description, setDescription] = useState(existing?.description ?? '');
  const [amountText, setAmountText] = useState(existing ? amountInputText(existing.amount) : '');
  const [currency, setCurrency] = useState(existing?.currency ?? tripCurrency);
  /** null: use the suggested (ECB) rate. */
  const [rateText, setRateText] = useState<string | null>(existing && existing.currency !== tripCurrency ? String(existing.rate) : null);
  const [paidBy, setPaidBy] = useState<string | undefined>(existing?.paidBy.userId ?? defaultPayer);
  const [category, setCategory] = useState<ExpenseCategory>(existing?.category ?? 'Food');
  const [date, setDate] = useState(() => (existing ? fromDateOnly(existing.date) : new Date()));
  const [splitMode, setSplitMode] = useState<SplitMode>(existing?.splitMode ?? 'Equal');
  const [equalIds, setEqualIds] = useState<string[]>(existing?.splitMode === 'Equal' ? existing.shares.map((s) => s.userId).filter((u) => everyone.includes(u)) : everyone);
  const [customTexts, setCustomTexts] = useState<Record<string, string>>(existing?.splitMode === 'Custom' ? customSharesInExpenseCurrency(existing) : {});
  const [pickingCurrency, setPickingCurrency] = useState(false);

  const dateOnly = toDateOnly(date);
  const foreign = currency !== tripCurrency;
  const suggestion = useExchangeRate(currency, tripCurrency, dateOnly, foreign);
  const suggestedRate = suggestion.data?.rate;
  const rate = !foreign ? 1 : rateText != null ? parseDecimal(rateText, 6) : (suggestedRate ?? null);
  const amount = parseDecimal(amountText);
  const converted = amount != null && rate != null && rate > 0 ? convert(amount, rate) : null;
  const solo = people.length <= 1;

  // Equal split: preview what each person pays (the server hands leftover cents out one by one).
  const splitCount = equalIds.length;
  const perPersonCents = converted != null && splitCount > 0 ? Math.floor(toCents(converted) / splitCount) : null;
  const unevenCents = converted != null && splitCount > 0 && toCents(converted) % splitCount !== 0;

  // Custom split: amounts in the expense's currency must add up to the amount exactly.
  const customAmounts = people.map((p) => ({ userId: p.userId, text: customTexts[p.userId] ?? '', value: customTexts[p.userId]?.trim() ? parseDecimal(customTexts[p.userId]) : 0 }));
  const customInvalid = customAmounts.some((c) => c.value == null);
  const leftCents = (amount != null ? toCents(amount) : 0) - customAmounts.reduce((sum, c) => sum + (c.value != null ? toCents(c.value) : 0), 0);

  const problem = !description.trim()
    ? 'Add a description'
    : description.trim().length > 120
      ? 'Keep the description under 120 characters'
      : amount == null || amount <= 0
        ? 'Enter an amount'
        : amount > MAX_AMOUNT
          ? 'An expense can be at most 1,000,000'
          : foreign && (rate == null || rate <= 0)
            ? 'Enter the exchange rate'
            : !paidBy
              ? 'Choose who paid'
              : splitMode === 'Equal' && splitCount === 0
                ? 'Choose who it’s split with'
                : splitMode === 'Custom' && customInvalid
                  ? 'Check the amounts per person'
                  : splitMode === 'Custom' && leftCents !== 0
                    ? leftCents > 0
                      ? 'Assign the whole amount'
                      : 'The shares add up to more than the amount'
                    : null;

  const existingId = existing?.id ?? null;
  const submit = () => {
    if (problem || amount == null || !paidBy) return;
    const custom = customAmounts.filter((c) => c.value != null && c.value > 0).map((c) => ({ userId: c.userId, amount: c.value ?? 0 }));
    const input: ExpenseInput = {
      description: description.trim(),
      category,
      paidBy,
      amount,
      currency,
      ...(foreign && rate != null ? { rate } : {}),
      date: dateOnly,
      splitMode,
      participants: splitMode === 'Equal' ? equalIds : custom.map((c) => c.userId),
      ...(splitMode === 'Custom' ? { customShares: custom } : {}),
    };
    save.mutate({ id: existingId, input }, { onSuccess: () => router.back(), onError: (e) => Alert.alert("Couldn't save the expense", e.message) });
  };
  const deleteExpense = () => {
    if (!existingId) return;
    confirmDelete('Delete this expense?', 'Balances are recalculated for everyone on the trip.', () =>
      remove.mutate(existingId, { onSuccess: () => router.back(), onError: (e) => Alert.alert("Couldn't delete", e.message) }),
    );
  };

  const nameOf = (userId: string, displayName: string) => (userId === myId ? 'You' : displayName || 'Former member');
  const toggle = (userId: string) => setEqualIds(equalIds.includes(userId) ? equalIds.filter((x) => x !== userId) : [...equalIds, userId]);

  return (
    <>
      <Input icon="pencil" placeholder="What was it? e.g. Hut dinner" value={description} onChangeText={setDescription} maxLength={120} autoFocus={!existing} returnKeyType="next" />

      <View style={styles.amountRow}>
        <View style={{ flex: 1 }}>
          <Input
            placeholder="0.00"
            keyboardType="decimal-pad"
            value={amountText}
            onChangeText={setAmountText}
            style={styles.amountInput}
            accessibilityLabel={`Amount in ${currency}`}
          />
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Currency ${currency}, change`}
          onPress={() => setPickingCurrency(true)}
          style={({ pressed }) => [styles.currencyChip, { backgroundColor: foreign ? theme.accentSoft : theme.backgroundElement, borderColor: theme.separator }, pressed && { opacity: 0.7 }]}>
          <ThemedText type="smallBold" style={{ color: foreign ? theme.accent : theme.text }}>
            {currency}
          </ThemedText>
          <Icon name="chevron.right" size={11} color={foreign ? theme.accent : theme.textSecondary} />
        </Pressable>
      </View>

      {foreign && (
        <Card>
          <View style={styles.inline}>
            <Icon name="dollarsign.circle" size={14} color={theme.textSecondary} />
            <ThemedText type="small" style={styles.tabular}>
              1 {currency} =
            </ThemedText>
            <View style={{ width: 130 }}>
              <Input
                keyboardType="decimal-pad"
                placeholder={suggestion.isLoading ? '…' : 'Rate'}
                value={rateText ?? (suggestedRate != null ? String(suggestedRate) : '')}
                onChangeText={setRateText}
                style={[styles.tabular, { textAlign: 'right', paddingVertical: 10 }]}
                accessibilityLabel={`${tripCurrency} per 1 ${currency}`}
              />
            </View>
            <ThemedText type="small">{tripCurrency}</ThemedText>
          </View>
          <RateSource
            loading={suggestion.isLoading}
            failed={!!suggestion.error && suggestedRate == null}
            source={suggestion.data ? `${suggestion.data.source}, ${formatShortDate(suggestion.data.date)}` : null}
            overridden={rateText != null && suggestedRate != null && parseDecimal(rateText, 6) !== suggestedRate}
            onUseSuggested={() => setRateText(null)}
          />
          {rateText != null && rate == null && (
            <ThemedText type="small" style={{ color: theme.danger }}>
              Enter the rate as a number, up to 6 decimals.
            </ThemedText>
          )}
          {converted != null && (
            <View style={styles.inline}>
              <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
                In {tripCurrency}
              </ThemedText>
              <ThemedText type="smallBold" style={styles.num}>
                {formatMoney(converted, tripCurrency)}
              </ThemedText>
            </View>
          )}
        </Card>
      )}

      {!solo && (
        <Section title="Paid by">
          <View style={styles.chips}>
            {people.map((p) => (
              <Chip key={p.userId} icon={p.userId === myId ? 'person.fill' : undefined} label={nameOf(p.userId, p.displayName)} selected={paidBy === p.userId} onPress={() => setPaidBy(p.userId)} />
            ))}
          </View>
        </Section>
      )}

      <Section title="Category">
        <View style={styles.chips}>
          {EXPENSE_CATEGORIES.map((c) => (
            <Chip key={c.value} icon={c.icon} label={c.label} selected={category === c.value} onPress={() => setCategory(c.value)} />
          ))}
        </View>
      </Section>

      <Card>
        <DateField
          label="Date"
          value={date}
          onChange={(d) => {
            setDate(d);
            if (foreign) setRateText(null); // suggest the rate for the new date
          }}
        />
      </Card>

      {!solo && (
        <Section title="Split">
          <Segmented
            value={splitMode}
            onChange={setSplitMode}
            options={[
              { value: 'Equal', label: 'Equally', icon: 'person.2' },
              { value: 'Custom', label: 'Custom amounts', icon: 'pencil' },
            ]}
          />
          {splitMode === 'Equal' ? (
            <>
              <List>
                {people.map((p) => {
                  const on = equalIds.includes(p.userId);
                  return (
                    <Pressable key={p.userId} onPress={() => toggle(p.userId)} accessibilityRole="checkbox" accessibilityState={{ checked: on }}>
                      <View style={styles.splitRow}>
                        <Icon name={on ? 'checkmark.circle.fill' : 'circle'} size={22} color={on ? theme.accent : theme.textSecondary} weight="regular" />
                        <ThemedText style={{ flex: 1 }} numberOfLines={1}>
                          {nameOf(p.userId, p.displayName)}
                        </ThemedText>
                        <ThemedText type="small" themeColor={on ? 'text' : 'textSecondary'} style={styles.num}>
                          {on && perPersonCents != null ? formatMoney(fromCents(perPersonCents), tripCurrency) : '–'}
                        </ThemedText>
                      </View>
                    </Pressable>
                  );
                })}
              </List>
              <View style={styles.inline}>
                <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
                  {splitCount === 0 ? 'Choose at least one person.' : `Split ${splitCount} way${splitCount === 1 ? '' : 's'}${unevenCents ? '; leftover cents go to some people' : ''}`}
                </ThemedText>
                <Pressable onPress={() => setEqualIds(equalIds.length === everyone.length ? [] : everyone)} hitSlop={8}>
                  <ThemedText type="smallBold" style={{ color: theme.accent }}>
                    {equalIds.length === everyone.length ? 'Clear' : 'Everyone'}
                  </ThemedText>
                </Pressable>
              </View>
            </>
          ) : (
            <>
              <List>
                {customAmounts.map((c) => {
                  const p = people.find((x) => x.userId === c.userId);
                  return (
                    <View key={c.userId} style={styles.splitRow}>
                      <ThemedText style={{ flex: 1 }} numberOfLines={1}>
                        {nameOf(c.userId, p?.displayName ?? '')}
                      </ThemedText>
                      <View style={{ width: 130 }}>
                        <Input
                          keyboardType="decimal-pad"
                          placeholder="0.00"
                          value={c.text}
                          onChangeText={(t) => setCustomTexts({ ...customTexts, [c.userId]: t })}
                          style={[styles.tabular, { textAlign: 'right', paddingVertical: 10 }, c.value == null && { color: theme.danger }]}
                          accessibilityLabel={`${nameOf(c.userId, p?.displayName ?? '')}'s share in ${currency}`}
                        />
                      </View>
                    </View>
                  );
                })}
              </List>
              <LeftToAssign leftCents={leftCents} currency={currency} hasAmount={amount != null && amount > 0} />
            </>
          )}
        </Section>
      )}

      <View style={{ gap: Spacing.two }}>
        <Button icon="checkmark" size="large" label={existing ? 'Save changes' : 'Add expense'} onPress={submit} loading={save.isPending} disabled={!!problem} />
        {problem && (amountText !== '' || description !== '') && (
          <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
            {problem}
          </ThemedText>
        )}
      </View>

      {existing?.canEdit && (
        <List>
          <ListRow title="Delete expense" destructive onPress={deleteExpense} />
        </List>
      )}

      <CurrencyPicker
        visible={pickingCurrency}
        title="Paid in"
        selected={currency}
        pinned={tripCurrency}
        onClose={() => setPickingCurrency(false)}
        onSelect={(code) => {
          setPickingCurrency(false);
          setCurrency(code);
          setRateText(null);
        }}
      />
    </>
  );
}

function RateSource({ loading, failed, source, overridden, onUseSuggested }: { loading: boolean; failed: boolean; source: string | null; overridden: boolean; onUseSuggested: () => void }) {
  const theme = useTheme();
  if (loading)
    return (
      <ThemedText type="caption" themeColor="textSecondary">
        Fetching the rate for this date…
      </ThemedText>
    );
  if (failed)
    return (
      <ThemedText type="caption" themeColor="textSecondary">
        {"Couldn't fetch a rate right now. Enter the rate you paid at."}
      </ThemedText>
    );
  if (!source) return null;
  return (
    <View style={styles.inline}>
      <ThemedText type="caption" themeColor="textSecondary" style={{ flex: 1 }}>
        {overridden ? 'Your rate' : source}
      </ThemedText>
      {overridden && (
        <Pressable onPress={onUseSuggested} hitSlop={8}>
          <ThemedText type="caption" style={{ color: theme.accent, fontWeight: 700 }}>
            Use {source}
          </ThemedText>
        </Pressable>
      )}
    </View>
  );
}

function LeftToAssign({ leftCents, currency, hasAmount }: { leftCents: number; currency: string; hasAmount: boolean }) {
  const theme = useTheme();
  const done = hasAmount && leftCents === 0;
  const color = done ? theme.accent : leftCents < 0 ? theme.danger : theme.textSecondary;
  return (
    <View style={[styles.left, { backgroundColor: done ? theme.accentSoft : leftCents < 0 ? theme.dangerSoft : theme.backgroundSelected }]}>
      <Icon name={done ? 'checkmark.circle.fill' : leftCents < 0 ? 'exclamationmark.triangle.fill' : 'circle.dotted'} size={14} color={color} />
      <ThemedText type="smallBold" style={{ color, flex: 1 }}>
        {!hasAmount ? 'Enter the amount first' : done ? 'All assigned' : leftCents > 0 ? 'Left to assign' : 'Over by'}
      </ThemedText>
      {hasAmount && !done && (
        <ThemedText type="smallBold" style={[styles.num, { color }]}>
          {formatMoney(fromCents(Math.abs(leftCents)), currency)}
        </ThemedText>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  amountRow: { flexDirection: 'row', alignItems: 'stretch', gap: Spacing.two },
  amountInput: { fontSize: 28, fontWeight: 700, fontVariant: ['tabular-nums'], textAlign: 'right', paddingVertical: 12 },
  currencyChip: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: Spacing.three, borderRadius: Radius.medium, borderCurve: 'continuous', borderWidth: StyleSheet.hairlineWidth },
  inline: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  splitRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4, minHeight: 52, paddingVertical: Spacing.one },
  left: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two, borderRadius: Radius.medium, borderCurve: 'continuous', paddingHorizontal: Spacing.three, paddingVertical: Spacing.three - 4 },
  num: { fontVariant: ['tabular-nums'], textAlign: 'right' },
  tabular: { fontVariant: ['tabular-nums'] },
});
