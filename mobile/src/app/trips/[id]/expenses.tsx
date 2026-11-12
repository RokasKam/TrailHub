import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useDeleteExpense, useDeletePayment, useRecordPayment, useTripExpenses } from '@/api/expenses';
import { useTripLiveUpdates } from '@/api/live-location';
import { useTrip } from '@/api/planning';
import { useSession } from '@/auth/session';
import { toDateOnly } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { balanceLine } from '@/components/trip-expenses-card';
import { Button, Card, Chip, EmptyState, Icon, Input, List, QueryState, Screen, Section, Segmented, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { confirmDelete, showActions } from '@/lib/action-sheet';
import { categoryInfo, formatMoney, fromCents, netColor, parseDecimal, toCents } from '@/lib/money';
import type { Balance, Expense, SettlementSuggestion, TripExpenses, TripPayment, TripPerson } from '@/lib/types';

type Tab = 'expenses' | 'balances';
type Entry = { kind: 'expense'; item: Expense } | { kind: 'payment'; item: TripPayment };

/** /trips/:id/expenses: everything logged on a trip, and who owes whom. */
export default function TripExpensesScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const theme = useTheme();
  const { user } = useSession();
  const trip = useTrip(id);
  const ledger = useTripExpenses(id);
  useTripLiveUpdates(id, trip.data?.groupId != null);
  const [tab, setTab] = useState<Tab>('expenses');

  const add = () => router.push(`/trips/${id}/expense`);
  const header = (
    <Stack.Screen
      options={{
        title: 'Expenses',
        headerRight: () => (
          <Pressable accessibilityRole="button" accessibilityLabel="Add expense" hitSlop={12} onPress={add}>
            <Icon name="plus" size={20} color={theme.accent} />
          </Pressable>
        ),
      }}
    />
  );

  const data = ledger.data;
  if (!data)
    return (
      <Screen>
        {header}
        <QueryState isLoading={ledger.isLoading} error={ledger.error} onRetry={() => void ledger.refetch()} />
      </Screen>
    );

  const myId = user?.id;
  const me = data.balances.find((b) => b.isMe);

  return (
    <Screen refreshing={ledger.isRefetching} onRefresh={() => void ledger.refetch()}>
      {header}
      {trip.data && (
        <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
          {trip.data.name} · amounts in {data.currency}
        </ThemedText>
      )}
      <QueryState error={ledger.error} hasData onRetry={() => void ledger.refetch()} />

      <Card>
        <StatRow>
          <Stat label="Total spent" value={formatMoney(data.total, data.currency)} icon="banknote.fill" />
          <Stat label="Expenses" value={String(data.expenses.length)} icon="list.bullet" />
        </StatRow>
        {me && data.participants.length > 1 && (
          <ThemedText type="smallBold" style={{ color: netColor(me.net, theme), fontVariant: ['tabular-nums'] }}>
            {balanceLine(me.net, data.currency)}
          </ThemedText>
        )}
      </Card>

      <Segmented
        value={tab}
        onChange={setTab}
        options={[
          { value: 'expenses', label: 'Expenses', icon: 'list.bullet' },
          { value: 'balances', label: 'Balances', icon: 'person.2' },
        ]}
      />

      {tab === 'expenses' ? <ExpenseList tripId={id} data={data} myId={myId} onAdd={add} /> : <Balances tripId={id} data={data} myId={myId} />}
    </Screen>
  );
}

const nameOf = (p: TripPerson, myId?: string) => (p.userId === myId ? 'You' : p.displayName || 'Former member');

// Expenses tab ------------------------------------------------------------------------------

function ExpenseList({ tripId, data, myId, onAdd }: { tripId: string; data: TripExpenses; myId?: string; onAdd: () => void }) {
  const removeExpense = useDeleteExpense(tripId);
  const removePayment = useDeletePayment(tripId);
  const fail = (title: string) => (e: Error) => Alert.alert(title, e.message);

  if (data.expenses.length === 0 && data.payments.length === 0) {
    return (
      <Card style={{ alignItems: 'center' }}>
        <EmptyState
          icon="banknote.fill"
          title="No expenses yet"
          message={data.participants.length > 1 ? 'Add what you paid for, like a hut night or the train, and choose who it’s split with.' : 'Log what this trip costs to keep track of your spending.'}
        />
        <Button icon="plus" label="Add an expense" onPress={onAdd} />
      </Card>
    );
  }

  // One timeline: expenses and payments, newest date first, grouped by day.
  const entries: Entry[] = [...data.expenses.map((item) => ({ kind: 'expense' as const, item })), ...data.payments.map((item) => ({ kind: 'payment' as const, item }))];
  entries.sort((a, b) => b.item.date.localeCompare(a.item.date) || b.item.createdAtUtc.localeCompare(a.item.createdAtUtc));
  const days: { date: string; entries: Entry[] }[] = [];
  for (const e of entries) {
    const last = days[days.length - 1];
    if (last?.date === e.item.date) last.entries.push(e);
    else days.push({ date: e.item.date, entries: [e] });
  }

  const expenseActions = (x: Expense) =>
    showActions(x.description, [
      { label: 'Edit', onPress: () => router.push(`/trips/${tripId}/expense?expenseId=${x.id}`) },
      {
        label: 'Delete',
        destructive: true,
        onPress: () => confirmDelete('Delete this expense?', `${x.description} · ${formatMoney(x.amountInTripCurrency, data.currency)}. Balances are recalculated.`, () => removeExpense.mutate(x.id, { onError: fail("Couldn't delete") })),
      },
    ]);
  const paymentActions = (p: TripPayment) =>
    showActions(`${nameOf(p.from, myId)} paid ${nameOf(p.to, myId)} ${formatMoney(p.amount, data.currency)}`, [
      {
        label: 'Delete payment',
        destructive: true,
        onPress: () => confirmDelete('Delete this payment?', 'The balances go back to before it was recorded.', () => removePayment.mutate(p.id, { onError: fail("Couldn't delete") })),
      },
    ]);

  return (
    <>
      {days.map((day) => (
        <Section key={day.date} title={formatDayTitle(day.date)} action={<DayTotal entries={day.entries} currency={data.currency} />}>
          <List>
            {day.entries.map((e) =>
              e.kind === 'expense' ? (
                <ExpenseRow
                  key={e.item.id}
                  expense={e.item}
                  tripCurrency={data.currency}
                  myId={myId}
                  onPress={e.item.canEdit ? () => router.push(`/trips/${tripId}/expense?expenseId=${e.item.id}`) : undefined}
                  onLongPress={e.item.canEdit ? () => expenseActions(e.item) : undefined}
                />
              ) : (
                <PaymentRow key={e.item.id} payment={e.item} currency={data.currency} myId={myId} onPress={e.item.canDelete ? () => paymentActions(e.item) : undefined} />
              ),
            )}
          </List>
        </Section>
      ))}
      <ThemedText type="caption" themeColor="textSecondary" style={{ textAlign: 'center' }}>
        Tap an expense to edit it, long-press to delete.
      </ThemedText>
    </>
  );
}

function formatDayTitle(date: string) {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
}

function DayTotal({ entries, currency }: { entries: Entry[]; currency: string }) {
  const cents = entries.reduce((sum, e) => sum + (e.kind === 'expense' ? toCents(e.item.amountInTripCurrency) : 0), 0);
  if (cents === 0) return null;
  return (
    <ThemedText type="small" themeColor="textSecondary" style={styles.num}>
      {formatMoney(fromCents(cents), currency)}
    </ThemedText>
  );
}

function ExpenseRow({ expense: x, tripCurrency, myId, onPress, onLongPress }: { expense: Expense; tripCurrency: string; myId?: string; onPress?: () => void; onLongPress?: () => void }) {
  const theme = useTheme();
  const cat = categoryInfo(x.category);
  const foreign = x.currency !== tripCurrency;
  const split = x.splitMode === 'Custom' ? 'custom split' : x.shares.length === 1 ? (x.shares[0].userId === x.paidBy.userId ? 'not split' : `for ${nameOf(x.shares[0], myId)}`) : `split ${x.shares.length} ways`;
  const mine = x.shares.find((s) => s.userId === myId);

  return (
    <Pressable onPress={onPress} onLongPress={onLongPress} disabled={!onPress && !onLongPress} accessibilityRole={onPress ? 'button' : undefined} style={({ pressed }) => pressed && { opacity: 0.6 }}>
      <View style={styles.row}>
        <View style={[styles.rowIcon, { backgroundColor: cat.color }]}>
          <Icon name={cat.icon} size={15} color="#fff" />
        </View>
        <View style={{ flex: 1 }}>
          <ThemedText numberOfLines={1}>{x.description}</ThemedText>
          <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
            {x.paidBy.userId === myId ? 'You paid' : `${x.paidBy.displayName || 'Former member'} paid`} · {split}
          </ThemedText>
        </View>
        <View style={styles.amount}>
          <ThemedText type="smallBold" style={styles.num}>
            {formatMoney(x.amountInTripCurrency, tripCurrency)}
          </ThemedText>
          {foreign ? (
            <ThemedText type="caption" themeColor="textSecondary" style={styles.num}>
              {formatMoney(x.amount, x.currency)}
            </ThemedText>
          ) : (
            mine &&
            x.shares.length > 1 && (
              <ThemedText type="caption" themeColor="textSecondary" style={styles.num}>
                yours {formatMoney(mine.amount, tripCurrency)}
              </ThemedText>
            )
          )}
        </View>
        {onPress && <Icon name="chevron.right" size={13} color={theme.textSecondary} />}
      </View>
    </Pressable>
  );
}

function PaymentRow({ payment: p, currency, myId, onPress }: { payment: TripPayment; currency: string; myId?: string; onPress?: () => void }) {
  return (
    <Pressable onPress={onPress} onLongPress={onPress} disabled={!onPress} accessibilityRole={onPress ? 'button' : undefined} style={({ pressed }) => pressed && { opacity: 0.6 }}>
      <View style={styles.row}>
        <View style={[styles.rowIcon, { backgroundColor: '#0EA5E9' }]}>
          <Icon name="arrow.left.arrow.right" size={15} color="#fff" />
        </View>
        <View style={{ flex: 1 }}>
          <ThemedText numberOfLines={1}>
            {nameOf(p.from, myId)} paid {p.to.userId === myId ? 'you' : p.to.displayName || 'Former member'}
          </ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            Payment
          </ThemedText>
        </View>
        <ThemedText type="smallBold" themeColor="textSecondary" style={styles.num}>
          {formatMoney(p.amount, currency)}
        </ThemedText>
      </View>
    </Pressable>
  );
}

// Balances tab ------------------------------------------------------------------------------

function Balances({ tripId, data, myId }: { tripId: string; data: TripExpenses; myId?: string }) {
  const record = useRecordPayment(tripId);
  const [recording, setRecording] = useState(false);
  const solo = data.participants.length <= 1 && data.balances.length <= 1;

  if (solo) {
    return (
      <Card style={{ alignItems: 'center' }}>
        <EmptyState icon="person.2" title="Just you on this trip" message="Share the trip with a group to split costs and see who owes whom." />
      </Card>
    );
  }

  const markPaid = (s: SettlementSuggestion) => {
    const text = `${nameOf(s.from, myId)} paid ${s.to.userId === myId ? 'you' : s.to.displayName} ${formatMoney(s.amount, data.currency)}`;
    Alert.alert('Mark as paid?', `${text}. This is recorded for everyone on the trip.`, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Mark paid',
        onPress: () =>
          record.mutate({ from: s.from.userId, to: s.to.userId, amount: s.amount, date: toDateOnly(new Date()) }, { onError: (e) => Alert.alert("Couldn't record the payment", e.message) }),
      },
    ]);
  };

  // Biggest creditors first, then debtors; you on top within ties.
  const balances = [...data.balances].sort((a, b) => b.net - a.net || Number(b.isMe) - Number(a.isMe));

  return (
    <>
      <Section title="Balances">
        <List>
          {balances.map((b) => (
            <BalanceRow key={b.userId} balance={b} currency={data.currency} />
          ))}
        </List>
      </Section>

      <Section title="Settle up">
        {data.suggestions.length === 0 ? (
          <Card style={{ alignItems: 'center' }}>
            <EmptyState icon="checkmark.seal.fill" title="All settled" message={data.expenses.length ? 'Nobody owes anybody anything.' : 'Once expenses are logged, the fewest payments to settle up show here.'} />
          </Card>
        ) : (
          <List>
            {data.suggestions.map((s) => (
              <SuggestionRow key={`${s.from.userId}-${s.to.userId}`} suggestion={s} currency={data.currency} myId={myId} busy={record.isPending} onMarkPaid={() => markPaid(s)} />
            ))}
          </List>
        )}
        {recording ? (
          <PaymentForm data={data} myId={myId} saving={record.isPending} onCancel={() => setRecording(false)} onSave={(body) => record.mutate(body, { onSuccess: () => setRecording(false), onError: (e) => Alert.alert("Couldn't record the payment", e.message) })} />
        ) : (
          <Button icon="plus" variant="secondary" label="Record another payment" onPress={() => setRecording(true)} />
        )}
      </Section>
    </>
  );
}

function BalanceRow({ balance: b, currency }: { balance: Balance; currency: string }) {
  const theme = useTheme();
  const cents = toCents(b.net);
  const color = netColor(b.net, theme);
  return (
    <View style={styles.row}>
      <View style={[styles.rowIcon, { backgroundColor: cents === 0 ? theme.backgroundSelected : color }]}>
        <Icon name={b.isMe ? 'person.fill' : 'person.crop.circle.fill'} size={15} color={cents === 0 ? theme.textSecondary : '#fff'} />
      </View>
      <View style={{ flex: 1 }}>
        <ThemedText numberOfLines={1}>{b.isMe ? 'You' : b.displayName || 'Former member'}</ThemedText>
        <ThemedText type="small" themeColor="textSecondary" numberOfLines={1} style={styles.tabular}>
          Paid {formatMoney(b.paid, currency)} · share {formatMoney(b.share, currency)}
        </ThemedText>
      </View>
      <View style={styles.amount}>
        <ThemedText type="smallBold" style={[styles.num, { color }]}>
          {cents === 0 ? formatMoney(0, currency) : `${cents > 0 ? '+' : '−'}${formatMoney(Math.abs(b.net), currency)}`}
        </ThemedText>
        <ThemedText type="caption" style={{ color }}>
          {cents > 0 ? 'is owed' : cents < 0 ? 'owes' : 'settled'}
        </ThemedText>
      </View>
    </View>
  );
}

function SuggestionRow({ suggestion: s, currency, myId, busy, onMarkPaid }: { suggestion: SettlementSuggestion; currency: string; myId?: string; busy: boolean; onMarkPaid: () => void }) {
  const theme = useTheme();
  const involvesMe = s.from.userId === myId || s.to.userId === myId;
  return (
    <View style={styles.row}>
      <View style={{ flex: 1 }}>
        <View style={styles.inline}>
          <ThemedText type="smallBold" numberOfLines={1} style={{ flexShrink: 1 }}>
            {nameOf(s.from, myId)}
          </ThemedText>
          <Icon name="arrow.right" size={12} color={theme.textSecondary} />
          <ThemedText type="smallBold" numberOfLines={1} style={{ flexShrink: 1 }}>
            {nameOf(s.to, myId)}
          </ThemedText>
        </View>
        {involvesMe && (
          <ThemedText type="caption" style={{ color: s.from.userId === myId ? theme.danger : theme.accent }}>
            {s.from.userId === myId ? 'You pay' : 'You get paid'}
          </ThemedText>
        )}
      </View>
      <ThemedText type="smallBold" style={[styles.num, { color: s.from.userId === myId ? theme.danger : s.to.userId === myId ? theme.accent : theme.text }]}>
        {formatMoney(s.amount, currency)}
      </ThemedText>
      <Button size="small" variant="tinted" icon="checkmark" label="Mark paid" disabled={busy} onPress={onMarkPaid} />
    </View>
  );
}

/** A payment that isn't one of the suggestions, e.g. a partial one or cash handed over on the trail. */
function PaymentForm({ data, myId, saving, onSave, onCancel }: { data: TripExpenses; myId?: string; saving: boolean; onSave: (body: { from: string; to: string; amount: number; date: string }) => void; onCancel: () => void }) {
  const theme = useTheme();
  const people = data.participants;
  const defaultFrom = people.some((p) => p.userId === myId) ? (myId ?? people[0]?.userId) : people[0]?.userId;
  const [from, setFrom] = useState<string | undefined>(defaultFrom);
  const [to, setTo] = useState<string | undefined>(people.find((p) => p.userId !== defaultFrom)?.userId);
  const [amountText, setAmountText] = useState('');

  const amount = parseDecimal(amountText);
  const problem = !from || !to ? 'Choose who paid whom' : from === to ? 'Choose two different people' : amount == null || amount <= 0 ? 'Enter an amount' : amount > 1_000_000 ? 'That’s more than 1,000,000' : null;
  const submit = () => {
    if (problem || !from || !to || amount == null) return;
    onSave({ from, to, amount, date: toDateOnly(new Date()) });
  };

  return (
    <Card>
      <ThemedText type="smallBold">Who paid</ThemedText>
      <View style={styles.chips}>
        {people.map((p) => (
          <Chip key={p.userId} label={nameOf(p, myId)} selected={from === p.userId} onPress={() => setFrom(p.userId)} />
        ))}
      </View>
      <ThemedText type="smallBold">To</ThemedText>
      <View style={styles.chips}>
        {people.map((p) => (
          <Chip key={p.userId} label={nameOf(p, myId)} selected={to === p.userId} onPress={() => setTo(p.userId)} />
        ))}
      </View>
      <Input icon="banknote.fill" placeholder={`Amount in ${data.currency}`} keyboardType="decimal-pad" value={amountText} onChangeText={setAmountText} style={styles.tabular} />
      {amountText !== '' && problem && (
        <ThemedText type="small" style={{ color: theme.danger }}>
          {problem}
        </ThemedText>
      )}
      <View style={styles.inline}>
        <View style={{ flex: 1 }}>
          <Button label="Cancel" variant="secondary" onPress={onCancel} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Record" loading={saving} disabled={!!problem} onPress={submit} />
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4, minHeight: 56, paddingVertical: Spacing.two },
  rowIcon: { width: 30, height: 30, borderRadius: 8, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center' },
  amount: { alignItems: 'flex-end' },
  num: { fontVariant: ['tabular-nums'], textAlign: 'right' },
  tabular: { fontVariant: ['tabular-nums'] },
  inline: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
});
