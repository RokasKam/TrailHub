// Live-location checks over SignalR, through the gateway. Needs the stack in AUTH_MODE=Development and
// the mobile app's node_modules (for @microsoft/signalr):
//   node scripts/signalr-test.mjs
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const mobile = fileURLToPath(new URL('../../mobile/package.json', import.meta.url));
const { HubConnectionBuilder, LogLevel } = createRequire(mobile)('@microsoft/signalr');

const base = process.env.BASE ?? 'http://localhost:8080';
const call = (method, path, user, body) =>
  fetch(base + path, { method, headers: { Authorization: `Dev ${user}`, 'Content-Type': 'application/json' }, body: body && JSON.stringify(body) })
    .then((r) => (r.status === 204 ? null : r.json()));
const connect = (user) =>
  new HubConnectionBuilder().withUrl(`${base}/hubs/groups`, { accessTokenFactory: () => user }).configureLogging(LogLevel.None).build();
const within = (promise, ms = 5000) => Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), ms))]);

let failed = 0;
const check = (name, ok) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}`);
  if (!ok) failed++;
};

const stamp = Date.now();
const a = `hub-a-${stamp}`, b = `hub-b-${stamp}`, stranger = `hub-x-${stamp}`;
const group = await call('POST', '/api/groups', a, { name: 'SignalR test' });
await call('POST', '/api/groups/join', b, { inviteCode: group.inviteCode });

const ha = connect(a), hb = connect(b), hx = connect(stranger);
await Promise.all([ha.start(), hb.start(), hx.start()]);
check('connected over WebSockets', ha.connection.transport.constructor.name === 'WebSocketTransport');
await ha.invoke('JoinGroup', group.id);
await hb.invoke('JoinGroup', group.id);

const viaHub = new Promise((resolve) => hb.on('LocationUpdated', (_g, loc) => loc.userId === a && resolve(loc)));
await ha.invoke('UpdateLocation', group.id, 49.25, 19.95, 1300, 5, 90);
check('member receives a location sent over the hub', (await within(viaHub)).latitude === 49.25);

check('snapshot on join includes it', (await hb.invoke('JoinGroup', group.id)).some((l) => l.userId === a));
check('non-member cannot join the channel', await hx.invoke('JoinGroup', group.id).then(() => false, () => true));
check('non-member cannot send locations', await hx.invoke('UpdateLocation', group.id, 1, 1, null, null, null).then(() => false, () => true));

const viaRest = new Promise((resolve) => hb.on('LocationUpdated', (_g, loc) => loc.latitude === 49.26 && resolve(loc)));
await call('POST', `/api/groups/${group.id}/locations/mine`, a, { latitude: 49.26, longitude: 19.96 });
check('REST-posted location (background path) reaches hub viewers', (await within(viaRest)).userId === a);

const removed = new Promise((resolve) => hb.on('LocationRemoved', (_g, userId) => resolve(userId)));
await call('DELETE', `/api/groups/${group.id}/locations/mine`, a);
check('stopping sharing notifies viewers', (await within(removed)) === a);

await Promise.all([ha.stop(), hb.stop(), hx.stop()]);
console.log(failed ? `\n${failed} check(s) failed` : '\nall SignalR checks passed');
process.exit(failed ? 1 : 0);
