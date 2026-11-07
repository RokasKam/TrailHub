// Routes across the app: sharing with a group (via the membership sync), copying, attaching to a trip, following on a
// recorded hike, and publishing to Discover (Routes → event → Trails). Needs the services in Auth:Mode=Development and
// a Mapbox token for routing:
//   node scripts/route-sharing-test.mjs
// GROUPS_URL, ROUTES_URL, TRIPS_URL, TRAILS_URL and ACTIVITY_URL default to the gateway.
const url = (name) => process.env[`${name}_URL`] ?? 'http://localhost:8080';
const call = (service, method, path, user, body) =>
  fetch(url(service) + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Group membership and published trails arrive over the message bus; retry until they have. */
async function eventually(fn, ok, ms = 15000) {
  const until = Date.now() + ms;
  for (;;) {
    const result = await fn();
    if (ok(result) || Date.now() > until) return result;
    await sleep(300);
  }
}

let failed = 0;
const check = (name, ok) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}`);
  if (!ok) failed++;
};

const stamp = Date.now();
const a = `route-a-${stamp}`, b = `route-b-${stamp}`, stranger = `route-x-${stamp}`;

const group = (await call('GROUPS', 'POST', '/api/groups', a, { name: 'Route test', displayName: 'Anna' })).body;
await call('GROUPS', 'POST', '/api/groups/join', b, { inviteCode: group.inviteCode, displayName: 'Ben' });

// Chamonix → Lac Blanc, with a night at the refuge: two days.
const saved = await call('ROUTES', 'POST', '/api/routes', a, {
  name: 'Lac Blanc overnight',
  waypoints: [
    { latitude: 45.9237, longitude: 6.8694, name: 'Chamonix' },
    { latitude: 45.98166, longitude: 6.89208, name: 'Refuge du Lac Blanc', isOvernight: true, kind: 'MountainHut' },
    { latitude: 45.9668, longitude: 6.9285, name: 'Argentière' },
  ],
});
check('author saves a two-day route', saved.status === 201 && saved.body.days.length === 2 && saved.body.isOwner);
const route = saved.body;
const r = `/api/routes/${route.id}`;

check('routes are private until shared (404)', (await call('ROUTES', 'GET', r, b)).status === 404);
const shared = await eventually(() => call('ROUTES', 'PUT', `${r}/groups/${group.id}`, a), (x) => x.status === 200);
check('author shares it with the group', shared.status === 200 && shared.body.sharedWith?.[0]?.name === 'Route test');

const seenByB = await call('ROUTES', 'GET', r, b);
check('members see it, read-only', seenByB.status === 200 && seenByB.body.isOwner === false);
check('it shows in their list as shared', (await call('ROUTES', 'GET', '/api/routes', b)).body.some((x) => x.id === route.id && !x.isOwner));
check('outsiders still get 404', (await call('ROUTES', 'GET', r, stranger)).status === 404);
check("members can't share it (403)", (await call('ROUTES', 'PUT', `${r}/groups/${group.id}`, b)).status === 403);
check("members can't publish it (403)", (await call('ROUTES', 'POST', `${r}/publish`, b, { name: 'x', region: 'y', difficulty: 'Easy' })).status === 403);
check('members can export it as GPX', (await fetch(`${url('ROUTES')}${r}/gpx`, { headers: { Authorization: `Dev ${b}` } })).status === 200);

const copy = await call('ROUTES', 'POST', `${r}/copy`, b);
check('members can copy it into their own routes', copy.status === 201 && copy.body.isOwner && copy.body.id !== route.id && copy.body.days.length === 2);

// Trips: attach the route; members see the link.
const trip = (await call('TRIPS', 'POST', '/api/trips', a, { name: 'Lac Blanc weekend', startDate: '2027-07-01', endDate: '2027-07-02' })).body;
const attached = await call('TRIPS', 'PUT', `/api/trips/${trip.id}/route`, a, { routeId: route.id });
check('a route can be attached to a trip', attached.status === 200 && attached.body.routeId === route.id);

// Hikes remember the route they followed.
const activityId = crypto.randomUUID();
const t0 = Date.now() - 3600_000;
const hike = await call('ACTIVITY', 'PUT', `/api/activities/${activityId}`, b, {
  name: 'Day 1',
  routeId: route.id,
  segments: [[0, 1, 2].map((i) => ({ latitude: 45.9237 + i * 0.001, longitude: 6.8694, elevation: 1040 + i * 10, timestampUtc: new Date(t0 + i * 60_000).toISOString() }))],
});
const details = await call('ACTIVITY', 'GET', `/api/activities/${activityId}`, b);
check('a hike recorded along the route links back to it', hike.status < 300 && details.body?.routeId === route.id);

// Publishing: Routes publishes an event, Trails creates a trail with the route's id.
check("straight-line or tiny routes can't be published", (await call('ROUTES', 'POST', `${r}/publish`, a, { name: '', region: 'Chamonix', difficulty: 'Moderate' })).status === 400);
const published = await call('ROUTES', 'POST', `${r}/publish`, a, { name: 'Lac Blanc via the refuge', description: 'Two days', region: 'Chamonix valley', difficulty: 'Moderate' });
check('author publishes it', published.status === 200 && published.body.publishedAtUtc);
const trail = await eventually(() => call('TRAILS', 'GET', `/api/trails/${route.id}`, stranger), (x) => x.status === 200);
check('it appears in Discover as a trail with the same id', trail.status === 200 && trail.body.name === 'Lac Blanc via the refuge' && trail.body.region === 'Chamonix valley');

await call('ROUTES', 'POST', `${r}/publish`, a, { name: 'Lac Blanc loop (updated)', region: 'Chamonix valley', difficulty: 'Hard' });
const updated = await eventually(() => call('TRAILS', 'GET', `/api/trails/${route.id}`, stranger), (x) => x.body?.name === 'Lac Blanc loop (updated)');
check('publishing again updates the same trail', updated.body?.name === 'Lac Blanc loop (updated)' && updated.body?.difficulty === 'Hard');

// Leaving the group removes access.
await call('GROUPS', 'POST', `/api/groups/${group.id}/leave`, b);
const gone = await eventually(() => call('ROUTES', 'GET', r, b), (x) => x.status === 404);
check('members who leave lose access', gone.status === 404);

// Clean up what the API can delete. (Published trails stay; the script prints the id.)
await call('ROUTES', 'DELETE', `/api/routes/${copy.body.id}`, b);
await call('TRIPS', 'DELETE', `/api/trips/${trip.id}`, a);
await call('ACTIVITY', 'DELETE', `/api/activities/${activityId}`, b);
await call('GROUPS', 'DELETE', `/api/groups/${group.id}`, a);
console.log(`\n(test route and published trail id: ${route.id})`);
console.log(failed ? `${failed} check(s) failed` : 'All route sharing checks passed');
process.exit(failed ? 1 : 0);
