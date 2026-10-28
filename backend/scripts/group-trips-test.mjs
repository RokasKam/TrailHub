// Shared group-trip checks: membership sync over RabbitMQ, access rules, claims, personal ticks and
// live updates over SignalR. Needs Groups and Trips in Auth:Mode=Development and the mobile app's
// node_modules (for @microsoft/signalr):
//   node scripts/group-trips-test.mjs
// GROUPS_URL and TRIPS_URL default to the gateway; point them at the services directly if needed.
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
const check = (name, ok) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}`);
  if (!ok) failed++;
};

const stamp = Date.now();
const a = `trip-a-${stamp}`, b = `trip-b-${stamp}`, c = `trip-c-${stamp}`, stranger = `trip-x-${stamp}`;

const group = (await groups('POST', '/api/groups', a, { name: 'Group trip test', displayName: 'Anna' })).body;
await groups('POST', '/api/groups/join', b, { inviteCode: group.inviteCode, displayName: 'Ben' });
await groups('POST', '/api/groups/join', c, { inviteCode: group.inviteCode, displayName: 'Cleo' });

const created = await eventually(
  () => trips('POST', '/api/trips', a, { name: 'Tatra traverse', startDate: '2027-07-01', endDate: '2027-07-03', groupId: group.id }),
  (r) => r.status === 201,
);
check('owner creates a trip shared with their group', created.status === 201 && created.body.groupName === 'Group trip test');
const trip = created.body;
const t = `/api/trips/${trip.id}`;

const listB = await eventually(() => trips('GET', '/api/trips', b), (r) => r.body?.some((x) => x.id === trip.id));
check('members see the trip in their list, not as owner', listB.body.some((x) => x.id === trip.id && !x.isOwner));
check("group's trip list includes it", (await trips('GET', `/api/trips?groupId=${group.id}`, c)).body.some((x) => x.id === trip.id));
check('outsiders get 404', (await trips('GET', t, stranger)).status === 404);
check("outsiders can't list the group's trips", (await trips('GET', `/api/trips?groupId=${group.id}`, stranger)).status === 404);
check("members can't change the plan (403)", (await trips('PUT', `${t}/dates`, b, { startDate: '2027-07-02', endDate: '2027-07-03' })).status === 403);

const withGear = (await trips('POST', `${t}/checklist`, a, { text: 'Stove', kind: 'Shared' })).body;
const stove = withGear.checklist.find((i) => i.text === 'Stove');
const withLamp = (await trips('POST', `${t}/checklist`, b, { text: 'Headlamp' })).body;
const lamp = withLamp.checklist.find((i) => i.text === 'Headlamp');
check('members add items', lamp?.kind === 'Personal' && lamp.addedBy === b);

// Live updates: Ben watches the trip, Anna claims the stove.
const hub = new HubConnectionBuilder().withUrl(`${tripsUrl}/hubs/trips`, { accessTokenFactory: () => b }).configureLogging(LogLevel.None).build();
const changed = new Promise((resolve) => hub.on('TripChanged', (id) => resolve(id)));
await hub.start();
await hub.invoke('WatchTrip', trip.id);
await trips('PUT', `${t}/checklist/${stove.id}/claim`, a, { claim: true });
check('watchers get TripChanged over SignalR', (await within(changed).catch(() => null)) === trip.id);
await hub.stop();

const outsiderHub = new HubConnectionBuilder().withUrl(`${tripsUrl}/hubs/trips`, { accessTokenFactory: () => stranger }).configureLogging(LogLevel.None).build();
await outsiderHub.start();
check("outsiders can't watch the trip", await outsiderHub.invoke('WatchTrip', trip.id).then(() => false, () => true));
await outsiderHub.stop();

const seenByB = (await trips('GET', t, b)).body;
check('claims show who is bringing it', seenByB.checklist.find((i) => i.id === stove.id).claimedByName === 'Anna');
check("can't claim someone else's gear (409)", (await trips('PUT', `${t}/checklist/${stove.id}/claim`, b, { claim: true })).status === 409);
check("can't tick someone else's gear (409)", (await trips('PUT', `${t}/checklist/${stove.id}`, b, { isDone: true })).status === 409);
check("can't remove other people's items (403)", (await trips('DELETE', `${t}/checklist/${stove.id}`, b)).status === 403);

await trips('PUT', `${t}/checklist/${lamp.id}`, b, { isDone: true });
const lampFor = async (user) => (await trips('GET', t, user)).body;
const forB = await lampFor(b), forA = await lampFor(a);
check('personal ticks are per person', forB.checklist.find((i) => i.id === lamp.id).isDone && !forA.checklist.find((i) => i.id === lamp.id).isDone);
check('packing progress counts each member', forA.members.find((m) => m.userId === b)?.personalDone === 1 && forA.members.length === 3);

// Two people claim the same new item at once: exactly one wins.
const tent = (await trips('POST', `${t}/checklist`, a, { text: 'Tent', kind: 'Shared' })).body.checklist.find((i) => i.text === 'Tent');
const race = await Promise.all([b, c].map((u) => trips('PUT', `${t}/checklist/${tent.id}/claim`, u, { claim: true })));
const winner = (await trips('GET', t, a)).body.checklist.find((i) => i.id === tent.id).claimedBy;
check('simultaneous claims: one wins, the other gets 409', race.filter((r) => r.status === 200).length >= 1 && race.some((r) => r.status === 409) && [b, c].includes(winner));

// Whoever won the tent leaves: their claim and ticks go, and they lose access.
await trips('PUT', `${t}/checklist/${lamp.id}`, winner, { isDone: true });
await groups('POST', `/api/groups/${group.id}/leave`, winner);
const gone = await eventually(() => trips('GET', t, winner), (r) => r.status === 404);
check('people who leave the group lose access', gone.status === 404);
const after = (await trips('GET', t, a)).body;
check('their claims are released', after.checklist.find((i) => i.id === tent.id).claimedBy === null);
check('they drop out of packing progress', !after.members.some((m) => m.userId === winner));

const priv = await trips('PUT', `${t}/group`, a, { groupId: null });
check('owner can make it private again', priv.status === 200 && priv.body.groupId === null);
const stayer = winner === b ? c : b;
check('then members lose access', (await trips('GET', t, stayer)).status === 404);

await groups('DELETE', `/api/groups/${group.id}`, a);
await trips('DELETE', t, a);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll group-trip checks passed');
process.exit(failed ? 1 : 0);
