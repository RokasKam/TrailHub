// Trip expenses: trip currency (and its lock), equal and custom splits with exact cents, foreign-currency expenses
// with a given rate, balances that add up to zero, settle-up suggestions and payments, who may edit or delete what,
// people who leave keeping their balance, and live updates over SignalR. Needs Groups and Trips in
// Auth:Mode=Development and the mobile app's node_modules (for @microsoft/signalr):
//   node scripts/trip-expenses-test.mjs
// GROUPS_URL and TRIPS_URL default to the gateway. The exchange-rate check accepts 503 so CI doesn't depend on the
// rate provider being reachable.
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const mobile = fileURLToPath(new URL('../../mobile/package.json', import.meta.url));
const { HubConnectionBuilder, LogLevel } = createRequire(mobile)('@microsoft/signalr');

const groupsUrl = process.env.GROUPS_URL ?? 'http://localhost:8080';
const tripsUrl = process.env.TRIPS_URL ?? 'http://localhost:8080';

const request = (base, method, path, user, body) =>
  fetch(base + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));
const groups = (...args) => request(groupsUrl, ...args);
const trips = (...args) => request(tripsUrl, ...args);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const within = (promise, ms = 5000) => Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), ms))]);

/** Membership reaches Trips asynchronously over the bus; retry until it has. */
async function eventually(fn, ok, ms = 10000) {
  const until = Date.now() + ms;
  for (;;) {
    const result = await fn();
    if (ok(result) || Date.now() > until) return result;
    await sleep(250);
  }
}

let failed = 0;
const check = (name, ok, detail) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}${!ok && detail !== undefined ? ` → ${JSON.stringify(detail).slice(0, 300)}` : ''}`);
  if (!ok) failed++;
};
const problem = (res, status, code) => res.status === status && res.body?.code === code;
const cents = (x) => Math.round(x * 100);
const sumCents = (xs) => xs.reduce((s, x) => s + cents(x), 0);

const stamp = Date.now();
// User ids sort a < b < c, which decides who gets leftover cents in an equal split.
const a = `exp-a-${stamp}`, b = `exp-b-${stamp}`, c = `exp-c-${stamp}`, stranger = `exp-x-${stamp}`;

const group = (await groups('POST', '/api/groups', a, { name: 'Expenses test', displayName: 'Anna' })).body;
await groups('POST', '/api/groups/join', b, { inviteCode: group.inviteCode, displayName: 'Ben' });
await groups('POST', '/api/groups/join', c, { inviteCode: group.inviteCode, displayName: 'Cleo' });

const created = await eventually(
  () => trips('POST', '/api/trips', a, { name: 'Haute Route', startDate: '2027-07-01', endDate: '2027-07-06', groupId: group.id }),
  (r) => r.status === 201,
);
const trip = created.body;
const t = `/api/trips/${trip.id}`;
check('new trips are in EUR with nothing spent', trip?.currency === 'EUR' && trip.expenseSummary?.total === 0 && trip.expenseSummary.expenseCount === 0, trip);
await eventually(() => trips('GET', t, c), (r) => r.status === 200 && r.body.members.length === 3);

// ── Currency ─────────────────────────────────────────────────────────────────────────────────────────────────
check("members can't set the currency (403)", problem(await trips('PUT', `${t}/currency`, b, { currency: 'CHF' }), 403, 'Trip.NotOwner'));
check('unknown currencies are rejected', problem(await trips('PUT', `${t}/currency`, a, { currency: 'XQZ' }), 400, 'Trip.InvalidCurrency'));
const chf = await trips('PUT', `${t}/currency`, a, { currency: 'CHF' });
check('owner sets the trip currency', chf.status === 200 && chf.body.currency === 'CHF', chf);
check('trip lists show the currency', (await trips('GET', '/api/trips', b)).body?.find((x) => x.id === trip.id)?.currency === 'CHF');

const currencies = await trips('GET', '/api/trips/currencies', a);
check('currencies list the ECB ones', currencies.status === 200 && ['EUR', 'CHF', 'USD'].every((code) => currencies.body.some((x) => x.code === code && x.name)), currencies);

const rate = await trips('GET', '/api/trips/exchange-rate?from=EUR&to=CHF&date=2026-06-28', a);
check(
  'exchange rate: a rate on or before the date, or 503 when the provider is down',
  (rate.status === 200 && rate.body.from === 'EUR' && rate.body.to === 'CHF' && rate.body.rate > 0 && rate.body.date <= '2026-06-28' && !!rate.body.source)
    || problem(rate, 503, 'Trip.RatesUnavailable'),
  rate,
);
check('exchange rate rejects bad codes', problem(await trips('GET', '/api/trips/exchange-rate?from=EU&to=CHF', a), 400, 'Trip.InvalidCurrency'));

// ── Expenses ─────────────────────────────────────────────────────────────────────────────────────────────────
const hub = new HubConnectionBuilder().withUrl(`${tripsUrl}/hubs/trips`, { accessTokenFactory: () => b }).configureLogging(LogLevel.None).build();
const changed = new Promise((resolve) => hub.on('TripChanged', (id) => resolve(id)));
await hub.start();
await hub.invoke('WatchTrip', trip.id);

const dinner = { description: 'Hut dinner', category: 'Food', paidBy: a, amount: 100, currency: 'CHF', date: '2027-07-01', splitMode: 'Equal', participants: [a, b, c] };
const e1 = await trips('POST', `${t}/expenses`, a, dinner);
check('equal split: shares add up, leftover cent to the first user id', e1.status === 201 && sumCents(e1.body.shares.map((s) => s.amount)) === 10000
  && e1.body.shares.find((s) => s.userId === a)?.amount === 33.34 && e1.body.shares.find((s) => s.userId === c)?.amount === 33.33, e1);
check('expense shows the payer and is editable by them', e1.body?.paidBy?.displayName === 'Anna' && e1.body.canEdit && e1.body.rate === 1 && e1.body.amountInTripCurrency === 100);
check('watchers get TripChanged when an expense is added', (await within(changed).catch(() => null)) === trip.id);
await hub.stop();

check('the currency is locked once something is logged (409)', problem(await trips('PUT', `${t}/currency`, a, { currency: 'EUR' }), 409, 'Trip.CurrencyLocked'));

const taxi = { description: 'Taxi to Arolla', category: 'Transport', paidBy: b, amount: 50, currency: 'EUR', rate: 0.95, date: '2027-07-02', splitMode: 'Custom', customShares: [{ userId: a, amount: 20 }, { userId: b, amount: 10 }, { userId: c, amount: 20 }] };
check('foreign currency needs a rate', problem(await trips('POST', `${t}/expenses`, b, { ...taxi, rate: undefined }), 400, 'Trip.RateRequired'));
check('rates must be positive', problem(await trips('POST', `${t}/expenses`, b, { ...taxi, rate: 0 }), 400, 'Trip.InvalidRate'));
check("custom amounts must add up", problem(await trips('POST', `${t}/expenses`, b, { ...taxi, customShares: [{ userId: a, amount: 20 }] }), 400, 'Trip.SplitMismatch'));
check('someone has to share the cost', problem(await trips('POST', `${t}/expenses`, b, { ...dinner, participants: [] }), 400, 'Trip.EmptySplit'));
check('only participants can pay', problem(await trips('POST', `${t}/expenses`, b, { ...dinner, paidBy: stranger }), 400, 'Trip.NotAParticipant'));
check('amounts must be positive', problem(await trips('POST', `${t}/expenses`, b, { ...dinner, amount: 0 }), 400, 'Trip.InvalidExpense'));
check('descriptions are required', problem(await trips('POST', `${t}/expenses`, b, { ...dinner, description: ' ' }), 400, 'Trip.InvalidExpense'));
check('expense currencies must be supported', problem(await trips('POST', `${t}/expenses`, b, { ...dinner, currency: 'XQZ', rate: 1 }), 400, 'Trip.InvalidCurrency'));

const e2 = await trips('POST', `${t}/expenses`, b, taxi);
check('foreign-currency expense is converted with the given rate', e2.status === 201 && e2.body.currency === 'EUR' && e2.body.rate === 0.95 && e2.body.amountInTripCurrency === 47.5
  && e2.body.splitMode === 'Custom' && sumCents(e2.body.shares.map((s) => s.amount)) === 4750 && e2.body.shares.find((s) => s.userId === b)?.amount === 9.5, e2);

// 3.33 / 3.34 / 3.33 EUR at 1.0612 round to 3.53 + 3.54 + 3.53 = 10.60, but the whole is 10.61: the largest share takes the cent.
const e3 = await trips('POST', `${t}/expenses`, c, { description: 'Coffee', category: 'Food', paidBy: c, amount: 10, currency: 'EUR', rate: 1.0612, date: '2027-07-03', splitMode: 'Custom', customShares: [{ userId: a, amount: 3.33 }, { userId: b, amount: 3.34 }, { userId: c, amount: 3.33 }] });
check('custom split rounding lands on the largest share', e3.status === 201 && e3.body.amountInTripCurrency === 10.61 && e3.body.shares.find((s) => s.userId === b)?.amount === 3.55
  && sumCents(e3.body.shares.map((s) => s.amount)) === 1061, e3);

// ── Who may change what ──────────────────────────────────────────────────────────────────────────────────────
check('outsiders get 404 on expenses', (await trips('GET', `${t}/expenses`, stranger)).status === 404);
check("outsiders can't add expenses (404)", (await trips('POST', `${t}/expenses`, stranger, dinner)).status === 404);
check("others can't edit someone's expense (403)", problem(await trips('PUT', `${t}/expenses/${e1.body.id}`, c, { ...dinner, amount: 1 }), 403, 'Trip.NotExpenseEditor'));
check("others can't delete it either (403)", problem(await trips('DELETE', `${t}/expenses/${e1.body.id}`, b), 403, 'Trip.NotExpenseEditor'));
const asBen = (await trips('GET', `${t}/expenses`, b)).body;
check('canEdit reflects the rules', asBen.expenses.find((e) => e.id === e1.body.id)?.canEdit === false && asBen.expenses.find((e) => e.id === e2.body.id)?.canEdit === true);
const ownerEdit = await trips('PUT', `${t}/expenses/${e2.body.id}`, a, { ...taxi, description: 'Taxi to Arolla and back' });
check("the owner can edit anyone's expense", ownerEdit.status === 200 && ownerEdit.body.description === 'Taxi to Arolla and back' && ownerEdit.body.amountInTripCurrency === 47.5, ownerEdit);
const payerEdit = await trips('PUT', `${t}/expenses/${e1.body.id}`, a, { ...dinner, amount: 99.99 });
check('the payer can edit their expense', payerEdit.status === 200 && sumCents(payerEdit.body.shares.map((s) => s.amount)) === 9999, payerEdit);
check('missing expenses are 404', problem(await trips('PUT', `${t}/expenses/00000000-0000-0000-0000-000000000001`, a, dinner), 404, 'Trip.ExpenseNotFound'));

// ── Balances and settling up ─────────────────────────────────────────────────────────────────────────────────
const ledger = (await trips('GET', `${t}/expenses`, a)).body;
check('expenses come newest date first', ledger.expenses.map((e) => e.date).join() === ['2027-07-03', '2027-07-02', '2027-07-01'].join());
check('total is everything spent', cents(ledger.total) === 9999 + 4750 + 1061, ledger.total);
check('balances add up to zero', sumCents(ledger.balances.map((x) => x.net)) === 0, ledger.balances);
check('balances are paid − share', ledger.balances.every((x) => cents(x.net) === cents(x.paid) - cents(x.share)) && ledger.balances.filter((x) => x.isMe).length === 1);
check('participants are the group members', ledger.participants.length === 3 && ledger.participants[0].userId === a);
check('suggestions settle everyone in at most n − 1 payments', ledger.suggestions.length > 0 && ledger.suggestions.length <= 2 && ledger.suggestions.every((s) => s.amount >= 0.01 && s.from.displayName && s.to.displayName), ledger.suggestions);
const summary = (await trips('GET', t, b)).body.expenseSummary;
check("the trip shows a summary with the caller's balance", summary.expenseCount === 3 && cents(summary.myNet) === cents(ledger.balances.find((x) => x.userId === b).net), summary);

check('payments need two different people', problem(await trips('POST', `${t}/payments`, b, { from: b, to: b, amount: 5, date: '2027-07-06' }), 400, 'Trip.InvalidPayment'));
check('payments need participants', problem(await trips('POST', `${t}/payments`, b, { from: b, to: stranger, amount: 5, date: '2027-07-06' }), 400, 'Trip.NotAParticipant'));
for (const s of ledger.suggestions) {
  const paid = await trips('POST', `${t}/payments`, s.from.userId, { from: s.from.userId, to: s.to.userId, amount: s.amount, date: '2027-07-06' });
  check(`"Mark paid": ${s.from.displayName} pays ${s.to.displayName} ${s.amount}`, paid.status === 201 && paid.body.amount === s.amount && paid.body.canDelete, paid);
}
const settled = (await trips('GET', `${t}/expenses`, c)).body;
check('after paying the suggestions everyone is settled', settled.balances.every((x) => x.net === 0) && settled.suggestions.length === 0 && settled.payments.length === ledger.suggestions.length, settled.balances);

const extra = (await trips('POST', `${t}/payments`, b, { from: b, to: a, amount: 1, date: '2027-07-06' })).body;
check("others can't delete a payment (403)", problem(await trips('DELETE', `${t}/payments/${extra.id}`, c), 403, 'Trip.NotPaymentEditor'));
check('the payee can delete it', (await trips('DELETE', `${t}/payments/${extra.id}`, a)).status === 204);
check('missing payments are 404', problem(await trips('DELETE', `${t}/payments/${extra.id}`, a), 404, 'Trip.PaymentNotFound'));

// ── People who leave keep their history ─────────────────────────────────────────────────────────────────────
const snacks = await trips('POST', `${t}/expenses`, c, { description: 'Snacks', category: 'Food', paidBy: c, amount: 9, currency: 'CHF', date: '2027-07-04', splitMode: 'Equal', participants: [a, b, c] });
check('a member logs one more expense', snacks.status === 201);
await groups('POST', `/api/groups/${group.id}/leave`, c);
check('people who leave lose access', (await eventually(() => trips('GET', `${t}/expenses`, c), (r) => r.status === 404)).status === 404);
const afterLeave = (await trips('GET', `${t}/expenses`, a)).body;
const cleo = afterLeave.balances.find((x) => x.userId === c);
check('their balance stays, with their last name', cleo?.displayName === 'Cleo' && cleo.net === 6 && !afterLeave.participants.some((p) => p.userId === c), afterLeave.balances);
check('balances still add up to zero', sumCents(afterLeave.balances.map((x) => x.net)) === 0);
check("they can't be added to new expenses", problem(await trips('POST', `${t}/expenses`, a, dinner), 400, 'Trip.NotAParticipant'));
check("the owner can delete anyone's expense", (await trips('DELETE', `${t}/expenses/${snacks.body.id}`, a)).status === 204);
check('then everyone is settled again', (await trips('GET', `${t}/expenses`, a)).body.balances.every((x) => x.net === 0));

// ── Private trips: a spending log for the owner ──────────────────────────────────────────────────────────────
const solo = (await trips('POST', '/api/trips', a, { name: 'Solo weekend', startDate: '2027-08-01', endDate: '2027-08-02' })).body;
const soloExpense = await trips('POST', `/api/trips/${solo.id}/expenses`, a, { description: 'Campsite', category: 'Accommodation', paidBy: a, amount: 25.5, currency: 'EUR', date: '2027-08-01', splitMode: 'Equal', participants: [a] });
const soloLedger = (await trips('GET', `/api/trips/${solo.id}/expenses`, a)).body;
check('private trips log expenses for the owner alone', soloExpense.status === 201 && soloLedger.participants.length === 1 && soloLedger.total === 25.5 && soloLedger.balances[0].net === 0, soloLedger);
check('deleting a trip deletes its expenses', (await trips('DELETE', `/api/trips/${solo.id}`, a)).status === 204);

await groups('DELETE', `/api/groups/${group.id}`, a);
await trips('DELETE', t, a);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll trip-expense checks passed');
process.exit(failed ? 1 : 0);
