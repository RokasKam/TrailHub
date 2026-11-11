// Trail moderation and route naming: reports, auto-hide after three, moderator review, author unpublish/publish
// (Routes → event → Trails). Needs Trails (with Moderation:ModeratorUserIds__0=<MODERATOR>) and Routes in
// Auth:Mode=Development, and a Mapbox token for routing:
//   MODERATOR=moderation-mod node scripts/moderation-test.mjs
// TRAILS_URL and ROUTES_URL default to the gateway.
import { readFile } from 'node:fs/promises';

const url = (name) => process.env[`${name}_URL`] ?? 'http://localhost:8080';
const moderator = process.env.MODERATOR ?? 'moderation-mod';
const call = (service, method, path, user, body) =>
  fetch(url(service) + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
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
const author = `mod-author-${stamp}`;
const name = `Moderation test ${stamp}`;
const inSearch = async () => (await call('TRAILS', 'GET', `/api/trails?q=${encodeURIComponent(name)}`, 'someone')).body?.items?.some((t) => t.name === name);

// A trail to report: imported from the sample GPX.
const form = new FormData();
form.append('file', new Blob([await readFile(new URL('../samples/demo-loop.gpx', import.meta.url))]), 'demo.gpx');
form.append('name', name);
form.append('region', 'Test region');
form.append('difficulty', 'Moderate');
const imported = await fetch(`${url('TRAILS')}/api/trails/import`, { method: 'POST', headers: { Authorization: `Dev ${author}` }, body: form });
const trailId = (await imported.json()).id;
check('author imports a trail', imported.status === 201 && trailId);
check('it shows in Discover', await inSearch());

check("authors can't report their own trail", (await call('TRAILS', 'POST', `/api/trails/${trailId}/report`, author, { reason: 'Spam' })).status === 400);
for (const [i, reason] of ['Dangerous', 'Inaccurate'].entries()) {
  await call('TRAILS', 'POST', `/api/trails/${trailId}/report`, `mod-reporter-${i}-${stamp}`, { reason, details: i === 0 ? 'Path closed after a rockfall' : null });
}
check('two reports keep it in Discover', await inSearch());
await call('TRAILS', 'POST', `/api/trails/${trailId}/report`, `mod-reporter-0-${stamp}`, { reason: 'Dangerous' });
check('reporting twice counts once', await inSearch());
await call('TRAILS', 'POST', `/api/trails/${trailId}/report`, `mod-reporter-2-${stamp}`, { reason: 'Spam' });
check('the third report takes it out of Discover', !(await inSearch()));
check('others get 404 for the hidden trail', (await call('TRAILS', 'GET', `/api/trails/${trailId}`, 'someone')).status === 404);
const seenByAuthor = await call('TRAILS', 'GET', `/api/trails/${trailId}`, author);
check('the author still sees it, marked hidden by reports', seenByAuthor.status === 200 && seenByAuthor.body.hiddenBy === 'Reports');

check('non-moderators get 403 for the queue', (await call('TRAILS', 'GET', '/api/trails/moderation', author)).status === 403);
const queue = await call('TRAILS', 'GET', '/api/trails/moderation', moderator);
const item = queue.body?.find?.((x) => x.trailId === trailId);
check('moderators see it with its 3 reports', queue.status === 200 && item?.openReports === 3 && item.hiddenBy === 'Reports');
check("non-moderators can't restore it (403)", (await call('TRAILS', 'PUT', `/api/trails/${trailId}/moderation`, author, { hidden: false })).status === 403);
await call('TRAILS', 'PUT', `/api/trails/${trailId}/moderation`, moderator, { hidden: false });
check('a moderator restores it to Discover', await inSearch());
const after = (await call('TRAILS', 'GET', '/api/trails/moderation', moderator)).body;
check('restoring resolves its reports', !after.some((x) => x.trailId === trailId));

// Author unpublish / publish again via Routes.
const route = (
  await call('ROUTES', 'POST', '/api/routes', author, {
    name: 'Unpublish test',
    waypoints: [
      { latitude: 45.9237, longitude: 6.8694 },
      { latitude: 45.9668, longitude: 6.9285 },
    ],
  })
).body;
const suggested = await call('ROUTES', 'POST', '/api/routes/suggest-name', author, { waypoints: route.waypoints });
check(`route names are suggested from the places ("${suggested.body?.name}")`, suggested.status === 200 && /Chamonix/.test(suggested.body.name));
await call('ROUTES', 'POST', `/api/routes/${route.id}/publish`, author, { name: `Unpublish ${stamp}`, region: 'Chamonix', difficulty: 'Moderate' });
await eventually(() => call('TRAILS', 'GET', `/api/trails/${route.id}`, 'someone'), (x) => x.status === 200);
await call('ROUTES', 'POST', `/api/routes/${route.id}/unpublish`, author);
const unpublished = await eventually(() => call('TRAILS', 'GET', `/api/trails/${route.id}`, 'someone'), (x) => x.status === 404);
check('unpublishing takes the trail out of Discover', unpublished.status === 404);
await call('ROUTES', 'POST', `/api/routes/${route.id}/publish`, author, { name: `Unpublish ${stamp}`, region: 'Chamonix', difficulty: 'Moderate' });
const back = await eventually(() => call('TRAILS', 'GET', `/api/trails/${route.id}`, 'someone'), (x) => x.status === 200);
check('publishing again brings it back', back.status === 200);

// Clean up: hide the test trails for good and delete the route.
await call('TRAILS', 'PUT', `/api/trails/${trailId}/moderation`, moderator, { hidden: true });
await call('TRAILS', 'PUT', `/api/trails/${route.id}/moderation`, moderator, { hidden: true });
await call('ROUTES', 'DELETE', `/api/routes/${route.id}`, author);
console.log(`\n(test trails: ${trailId}, ${route.id})`);
console.log(failed ? `${failed} check(s) failed` : 'All moderation checks passed');
process.exit(failed ? 1 : 0);
