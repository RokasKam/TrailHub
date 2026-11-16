// Notifications: the inbox fills from events of other services. Joining a group, sharing, liking (batched per hike)
// and commenting on hikes, trip expenses and payments, and scheduled group hikes reach the right people, and never
// the person who acted. Also preferences, read / read-all, the unread count and push device registration. Needs
// Groups, Activity, Trips and Notifications in Auth:Mode=Development (push may be disabled, PUSH_ENABLED=false):
//   node scripts/notifications-test.mjs
// BASE defaults to the gateway.
const base = process.env.BASE ?? 'http://localhost:8080';
const call = (method, path, user, body) =>
  fetch(base + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Notifications are written asynchronously from bus events; retry until they arrive. */
async function eventually(fn, ok, ms = 15000) {
  const until = Date.now() + ms;
  for (;;) {
    const result = await fn();
    if (ok(result) || Date.now() > until) return result;
    await sleep(300);
  }
}

let failed = 0;
const check = (name, ok, detail) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}${!ok && detail !== undefined ? ` → ${JSON.stringify(detail).slice(0, 400)}` : ''}`);
  if (!ok) failed++;
};
const problem = (res, status, code) => res.status === status && res.body?.code === code;

/** The user's inbox (first 100 items). */
const inbox = async (user) => (await call('GET', '/api/notifications?pageSize=100', user)).body?.items ?? [];
const ofKind = (items, kind, url) => items.filter((n) => n.kind === kind && (!url || n.targetUrl === url));
/** Waits until the user has at least `count` items of a kind (optionally for one target), and returns them. */
const waitFor = async (user, kind, count = 1, url) => ofKind(await eventually(() => inbox(user), (items) => ofKind(items, kind, url).length >= count), kind, url);

const stamp = Date.now();
const anna = `notif-a-${stamp}`, ben = `notif-b-${stamp}`, cleo = `notif-c-${stamp}`;

// ── Preferences ──────────────────────────────────────────────────────────────────────────────────────────────
const defaults = await call('GET', '/api/notifications/preferences', ben);
check('preferences are all on by default', defaults.status === 200 && defaults.body.likesComments && defaults.body.group && defaults.body.trips, defaults);
const changed = await call('PUT', '/api/notifications/preferences', ben, { likesComments: false, group: true, trips: false });
check('preferences can be changed', changed.status === 200 && !changed.body.likesComments && changed.body.group && !changed.body.trips, changed);
const reread = (await call('GET', '/api/notifications/preferences', ben)).body;
check('preferences round-trip', reread && !reread.likesComments && reread.group && !reread.trips, reread);

// ── Devices ──────────────────────────────────────────────────────────────────────────────────────────────────
const token = `ExponentPushToken[notif-${stamp}]`;
check('a device registers (204)', (await call('PUT', '/api/notifications/devices', ben, { token, platform: 'ios' })).status === 204);
check('registering again is fine (204)', (await call('PUT', '/api/notifications/devices', ben, { token, platform: 'ios' })).status === 204);
check('platform must be ios or android', problem(await call('PUT', '/api/notifications/devices', ben, { token, platform: 'windows' }), 400, 'Notification.InvalidDevice'));
check('a token is required', problem(await call('PUT', '/api/notifications/devices', ben, { platform: 'android' }), 400, 'Notification.InvalidDevice'));
check('the token moves to whoever registers it (204)', (await call('PUT', '/api/notifications/devices', anna, { token, platform: 'android' })).status === 204);
check("deleting someone else's token is ignored (204)", (await call('DELETE', `/api/notifications/devices/${encodeURIComponent(token)}`, ben)).status === 204);
check('signing out deletes the token (204)', (await call('DELETE', `/api/notifications/devices/${encodeURIComponent(token)}`, anna)).status === 204);
check('deleting an unknown token is fine (204)', (await call('DELETE', `/api/notifications/devices/${encodeURIComponent(token)}`, anna)).status === 204);

// ── Joining a group ──────────────────────────────────────────────────────────────────────────────────────────
const group = (await call('POST', '/api/groups', anna, { name: 'Notify friends', displayName: 'Anna' })).body;
// Joins are found by comparing snapshots, so let Notifications see the new group before anyone joins.
await sleep(2000);
await call('POST', '/api/groups/join', ben, { inviteCode: group.inviteCode, displayName: 'Ben' });
const benJoined = await waitFor(anna, 'GroupMemberJoined');
check('the owner hears that Ben joined', benJoined.length === 1 && benJoined[0].body === 'Ben joined Notify friends' && benJoined[0].targetUrl === `/groups/${group.id}`
  && benJoined[0].category === 'Group' && benJoined[0].read === false, benJoined);
await call('POST', '/api/groups/join', cleo, { inviteCode: group.inviteCode, displayName: 'Cleo' });
const cleoJoinedBen = await waitFor(ben, 'GroupMemberJoined');
check('existing members hear that Cleo joined', cleoJoinedBen[0]?.body === 'Cleo joined Notify friends', cleoJoinedBen);
check('the owner too', (await waitFor(anna, 'GroupMemberJoined', 2)).length === 2);
check("the creator's own group doesn't notify them, and joiners aren't told about themselves", ofKind(await inbox(cleo), 'GroupMemberJoined').length === 0);

// ── Sharing a hike ───────────────────────────────────────────────────────────────────────────────────────────
const id = crypto.randomUUID();
const t0 = Date.now() - 3 * 3600_000;
const points = Array.from({ length: 30 }, (_, i) => ({ latitude: 49.2 + i * 0.0005, longitude: 20.0, elevation: 1500 + i * 5, timestampUtc: new Date(t0 + i * 60_000).toISOString() }));
await call('PUT', `/api/activities/${id}`, anna, { name: 'Rysy ridge', segments: [points] });
await eventually(() => call('GET', '/api/activities/share-targets', anna), (r) => r.body?.some((g) => g.groupId === group.id && g.memberCount === 3));
const shared = await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [group.id], privacy: 'Full' });
check('the hike is shared', shared.status === 200, shared);
const feedUrl = `/feed/${id}`;
const benShared = await waitFor(ben, 'HikeShared', 1, feedUrl);
check('members get a "shared" item', benShared[0]?.title === 'New hike in Notify friends' && benShared[0].body === 'Anna shared Rysy ridge', benShared);
check('every member', (await waitFor(cleo, 'HikeShared', 1, feedUrl)).length === 1);

// ── Likes and comments ───────────────────────────────────────────────────────────────────────────────────────
await call('PUT', `/api/activities/feed/${id}/like`, ben);
let likes = await waitFor(anna, 'HikeLiked', 1, feedUrl);
check('the owner gets a like item', likes.length === 1 && likes[0].body === 'Ben liked your hike Rysy ridge' && likes[0].category === 'LikesComments', likes);
await call('POST', `/api/notifications/${likes[0].id}/read`, anna);
await call('PUT', `/api/activities/feed/${id}/like`, cleo);
likes = ofKind(await eventually(() => inbox(anna), (items) => ofKind(items, 'HikeLiked', feedUrl)[0]?.body?.startsWith('Cleo')), 'HikeLiked', feedUrl);
check('likes on one hike are batched into one item', likes.length === 1 && likes[0].body === 'Cleo and 1 other liked your hike Rysy ridge', likes);
check('the batch is unread again', likes[0]?.read === false);
check('the batch moved to the top', (await inbox(anna))[0]?.id === likes[0]?.id);

await call('PUT', `/api/activities/feed/${id}/like`, anna);
await call('POST', `/api/activities/feed/${id}/comments`, anna, { text: 'My own comment' });
await call('POST', `/api/activities/feed/${id}/comments`, ben, { text: 'What a view!' });
const comments = await waitFor(anna, 'HikeCommented', 1, feedUrl);
check('the owner gets a comment item', comments.length === 1 && comments[0].body === 'Ben commented on Rysy ridge: “What a view!”' && comments[0].title === 'New comment', comments);
await sleep(1500);
const afterSelf = await inbox(anna);
check("liking your own hike doesn't notify you", ofKind(afterSelf, 'HikeLiked', feedUrl)[0]?.body === 'Cleo and 1 other liked your hike Rysy ridge', ofKind(afterSelf, 'HikeLiked'));
check("commenting on your own hike doesn't notify you", ofKind(afterSelf, 'HikeCommented', feedUrl).length === 1);
check("members aren't told about likes on others' hikes", ofKind(await inbox(cleo), 'HikeLiked').length === 0);

// ── Trip expenses and payments ───────────────────────────────────────────────────────────────────────────────
const trip = (await eventually(
  () => call('POST', '/api/trips', anna, { name: 'Tatra traverse', startDate: '2027-07-01', endDate: '2027-07-03', groupId: group.id }),
  (r) => r.status === 201,
)).body;
await eventually(() => call('GET', `/api/trips/${trip.id}`, cleo), (r) => r.status === 200 && r.body.members?.length === 3);
const expense = await call('POST', `/api/trips/${trip.id}/expenses`, ben, { description: 'Hut dinner', category: 'Food', paidBy: ben, amount: 90, currency: 'EUR', date: '2027-07-01', splitMode: 'Equal', participants: [anna, ben, cleo] });
check('an expense is added', expense.status === 201, expense);
const expensesUrl = `/trips/${trip.id}/expenses`;
const annaExpense = await waitFor(anna, 'TripExpenseAdded', 1, expensesUrl);
check('share holders get the expense with their share', annaExpense[0]?.body === 'Ben added Hut dinner · your share €30.00' && annaExpense[0].title === 'Tatra traverse' && annaExpense[0].category === 'Trips', annaExpense);
check('every share holder', (await waitFor(cleo, 'TripExpenseAdded', 1, expensesUrl)).length === 1);

const payment = await call('POST', `/api/trips/${trip.id}/payments`, cleo, { from: cleo, to: ben, amount: 30, date: '2027-07-02' });
check('a payment is recorded', payment.status === 201, payment);
const benPaid = await waitFor(ben, 'TripPaymentRecorded', 1, expensesUrl);
check('the person paid hears about it', benPaid[0]?.body === 'Cleo paid you €30.00', benPaid);
await sleep(1500);
check("the expense creator isn't told about their own expense", ofKind(await inbox(ben), 'TripExpenseAdded').length === 0);
check("the payer who recorded it isn't told", ofKind(await inbox(cleo), 'TripPaymentRecorded').length === 0);
check("people outside the payment aren't told", ofKind(await inbox(anna), 'TripPaymentRecorded').length === 0);

// ── Group hikes ──────────────────────────────────────────────────────────────────────────────────────────────
const scheduled = await call('POST', `/api/groups/${group.id}/hikes`, anna, { title: 'Sunrise on Rysy', scheduledAtUtc: new Date(Date.now() + 3 * 86400_000).toISOString() });
check('a group hike is scheduled', scheduled.status === 201, scheduled);
const benHike = await waitFor(ben, 'GroupHikeScheduled');
check('members hear about the group hike', benHike[0]?.title === 'Group hike in Notify friends' && benHike[0].body.startsWith('Anna scheduled Sunrise on Rysy for ') && benHike[0].targetUrl === `/groups/${group.id}`, benHike);
check('every member', (await waitFor(cleo, 'GroupHikeScheduled')).length === 1);
await sleep(1000);
check("the scheduler isn't told", ofKind(await inbox(anna), 'GroupHikeScheduled').length === 0);

// ── Reading ──────────────────────────────────────────────────────────────────────────────────────────────────
const benItems = await inbox(ben);
const unread = (await call('GET', '/api/notifications/unread-count', ben)).body;
check('the unread count matches the inbox', unread?.count === benItems.filter((n) => !n.read).length && unread.count >= 4, { unread, items: benItems.length });
const page = await call('GET', '/api/notifications?page=1&pageSize=2', ben);
check('the inbox pages, newest first', page.status === 200 && page.body.items.length === 2 && page.body.totalCount === benItems.length
  && page.body.items[0].updatedAtUtc >= page.body.items[1].updatedAtUtc && page.body.pageSize === 2, page.body);
const first = benItems[0];
check('marking one read (204)', (await call('POST', `/api/notifications/${first.id}/read`, ben)).status === 204);
check('the unread count goes down', (await call('GET', '/api/notifications/unread-count', ben)).body?.count === unread.count - 1);
check('it shows as read', (await inbox(ben)).find((n) => n.id === first.id)?.read === true);
check("others can't read my items (404)", problem(await call('POST', `/api/notifications/${first.id}/read`, cleo), 404, 'Notification.NotFound'));
check('unknown ids are not found (404)', problem(await call('POST', `/api/notifications/${crypto.randomUUID()}/read`, ben), 404, 'Notification.NotFound'));
check('mark all read (204)', (await call('POST', '/api/notifications/read-all', ben)).status === 204);
check('nothing is unread', (await call('GET', '/api/notifications/unread-count', ben)).body?.count === 0);
check('everything shows as read', (await inbox(ben)).every((n) => n.read));
check("others' items stay unread", (await call('GET', '/api/notifications/unread-count', cleo)).body?.count > 0);
check('signed out → 401', (await fetch(`${base}/api/notifications`)).status === 401);

// Ben's switches were off for likes and trips: the inbox still got everything (pushes are logged, not checked here).
await call('PUT', '/api/notifications/preferences', ben, { likesComments: true, group: true, trips: true });

await call('DELETE', `/api/trips/${trip.id}`, anna);
await call('DELETE', `/api/activities/${id}`, anna);
await call('DELETE', `/api/groups/${group.id}`, anna);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll notification checks passed');
process.exit(failed ? 1 : 0);
