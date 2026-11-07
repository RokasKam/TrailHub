// Gear lists: starter lists, your own lists, adding a list to a trip (duplicates skipped, group gear personal on
// private trips), suggestions from the trip, and saving a checklist as a list. Needs Trips in Auth:Mode=Development:
//   node scripts/gear-lists-test.mjs
// TRIPS_URL defaults to the gateway.
const base = process.env.TRIPS_URL ?? 'http://localhost:8080';
const call = (method, path, user, body) =>
  fetch(base + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));

let failed = 0;
const check = (name, ok) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}`);
  if (!ok) failed++;
};

const me = `gear-${Date.now()}`;
const lists = await call('GET', '/api/trips/gear-lists', me);
check('everyone starts with the starter lists', lists.status === 200 && ['starter:day-hike', 'starter:hut-trip', 'starter:camping'].every((id) => lists.body.some((l) => l.id === id && l.isStarter)));

const mine = await call('POST', '/api/trips/gear-lists', me, {
  name: 'My hut kit',
  items: [
    { text: 'Liner', kind: 'Personal' },
    { text: ' liner ', kind: 'Personal' },
    { text: 'Stove', kind: 'Shared' },
  ],
});
check('a list keeps one of each item', mine.status === 200 && mine.body.items.length === 2);
check("others can't see or change your list", (await call('PUT', `/api/trips/gear-lists/${mine.body.id}`, 'someone-else', { name: 'x', items: [] })).status === 404);

const trip = (await call('POST', '/api/trips', me, { name: 'Hut weekend', startDate: '2027-07-01', endDate: '2027-07-03' })).body;
const t = `/api/trips/${trip.id}`;
await call('POST', `${t}/checklist`, me, { text: 'liner' });
const afterList = (await call('POST', `${t}/checklist/from-list/${mine.body.id}`, me)).body;
check('adding a list skips items already there', afterList.checklist.filter((i) => i.text.toLowerCase() === 'liner').length === 1);
check('group gear becomes personal on a private trip', afterList.checklist.find((i) => i.text === 'Stove')?.kind === 'Personal');
await call('PUT', `${t}/days/1`, me, { overnight: 'Refuge du Lac Blanc' });
const guessed = (await call('POST', `${t}/gear-suggestions`, me, {})).body;
check('suggestions read the night from the overnight name', guessed.some((s) => s.reason === '1 night in huts'));
const afterStarter = (await call('POST', `${t}/checklist/from-list/starter:hut-trip`, me)).body;
check('starter lists can be added too', afterStarter.checklist.some((i) => i.text === 'Earplugs'));

const suggestions = (await call('POST', `${t}/gear-suggestions`, me, { tentNights: 2, maxElevationMeters: 2900, longestDayMinutes: 420 })).body;
check('route facts add altitude, camping and long-day items', ['Microspikes', 'Tent', 'Water (2+ L)'].every((x) => suggestions.some((s) => s.text === x)));
check("suggestions skip what's on the checklist", !suggestions.some((s) => s.text === 'Earplugs'));

const bulk = (await call('POST', `${t}/checklist/bulk`, me, { items: suggestions.slice(0, 2).map(({ text, kind }) => ({ text, kind })) })).body;
check('picked suggestions are added', suggestions.slice(0, 2).every((s) => bulk.checklist.some((i) => i.text === s.text)));

const saved = await call('POST', `${t}/checklist/save-as-list`, me, { name: 'From my hut weekend' });
check("a trip's checklist can be saved as a list", saved.status === 200 && saved.body.items.length === bulk.checklist.length);

await call('DELETE', `/api/trips/gear-lists/${mine.body.id}`, me);
await call('DELETE', `/api/trips/gear-lists/${saved.body.id}`, me);
check('lists can be deleted', !(await call('GET', '/api/trips/gear-lists', me)).body.some((l) => !l.isStarter));
check('the trip keeps its items', (await call('GET', t, me)).body.checklist.length === bulk.checklist.length);
await call('DELETE', t, me);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll gear list checks passed');
process.exit(failed ? 1 : 0);
