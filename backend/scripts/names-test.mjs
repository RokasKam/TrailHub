// Readable names: people whose sign-in name is their email (Auth0 email/password users) are never shown by it.
// Checks the profile name, group member names, and that renaming a profile reaches the group and a group trip.
// Needs the stack in Auth:Mode=Development, which takes the name and email claims from X-Dev-Name / X-Dev-Email:
//   node scripts/names-test.mjs
const baseUrl = process.env.API_URL ?? 'http://localhost:8080';

/** `user` is { id, email }: signs in like an email/password user, whose name claim is their email. */
const call = (method, path, user, body) =>
  fetch(baseUrl + path, {
    method,
    headers: { Authorization: `Dev ${user.id}`, 'X-Dev-Name': user.email, 'X-Dev-Email': user.email, 'Content-Type': 'application/json' },
    body: body && JSON.stringify(body),
  }).then(async (r) => ({ status: r.status, body: r.status === 204 ? null : await r.json().catch(() => null) }));

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Renames travel Profile → Groups → Trips over the bus; retry until they have arrived. */
async function eventually(fn, ok, ms = 15000) {
  const until = Date.now() + ms;
  for (;;) {
    const result = await fn();
    if (ok(result) || Date.now() > until) return result;
    await sleep(250);
  }
}

let failed = 0;
const check = (name, ok, detail) => {
  console.log(`${ok ? '  ✓' : '  ✗'} ${name}`);
  if (!ok) {
    failed++;
    if (detail !== undefined) console.log('     ', JSON.stringify(detail));
  }
};

const stamp = Date.now();
const anna = { id: `names-anna-${stamp}`, email: `anna.k_smith+hikes${stamp}@example.com` };
const ben = { id: `names-ben-${stamp}`, email: `ben_99.${stamp}@example.com` };
const noEmail = (value) => typeof value === 'string' && !value.includes('@');
const nameOf = (members, user) => members?.find((m) => m.userId === user.id)?.displayName;

console.log('Profile');
const annaProfile = (await call('GET', '/api/profiles/me', anna)).body;
check('an email name becomes a readable profile name', annaProfile?.displayName === 'Anna K Smith', annaProfile);
check('the email itself is kept', annaProfile?.email === anna.email);
check('the name is not confirmed yet', annaProfile?.nameConfirmed === false);

console.log('Groups');
// Anna sends no name (Groups falls back to the token's email name); Ben's app sends his email as the name.
const created = await call('POST', '/api/groups', anna, { name: 'Names test' });
check('the owner joins with a readable name', created.status === 201 && nameOf(created.body?.members, anna) === 'Anna K Smith', created.body);
const group = created.body;
const joined = await call('POST', '/api/groups/join', ben, { inviteCode: group?.inviteCode, displayName: ben.email });
check('a member who joins with an email gets a readable name', joined.status === 200 && nameOf(joined.body?.members, ben) === 'Ben', joined.body);
check('no member is shown by an email', joined.body?.members?.every((m) => noEmail(m.displayName)));

console.log('Trips');
const trip = (
  await eventually(
    () => call('POST', '/api/trips', anna, { name: 'Names trip', startDate: '2027-08-01', endDate: '2027-08-02', groupId: group?.id }),
    (r) => r.status === 201,
  )
).body;
const t = `/api/trips/${trip?.id}`;
const before = await eventually(() => call('GET', t, ben), (r) => r.status === 200 && r.body?.members?.length === 2);
check('the trip lists members by their readable names', nameOf(before.body?.members, anna) === 'Anna K Smith' && nameOf(before.body?.members, ben) === 'Ben', before.body?.members);

console.log('Renaming');
const renamed = await call('PUT', '/api/profiles/me', ben, { displayName: '  Benjamin  ', bio: null, homeRegion: null, avatarUrl: null });
check('saving the name confirms it', renamed.status === 200 && renamed.body?.displayName === 'Benjamin' && renamed.body.nameConfirmed === true, renamed.body);
const groupAfter = await eventually(() => call('GET', `/api/groups/${group?.id}`, anna), (r) => nameOf(r.body?.members, ben) === 'Benjamin');
check('the new name reaches the group', nameOf(groupAfter.body?.members, ben) === 'Benjamin', groupAfter.body?.members);
const tripAfter = await eventually(() => call('GET', t, anna), (r) => nameOf(r.body?.members, ben) === 'Benjamin');
check("the new name reaches the trip's member list", nameOf(tripAfter.body?.members, ben) === 'Benjamin', tripAfter.body?.members);

const kept = await call('PUT', '/api/profiles/me', anna, { displayName: annaProfile?.displayName, bio: null, homeRegion: null, avatarUrl: null });
check('"Keep this name" confirms it unchanged', kept.status === 200 && kept.body?.displayName === 'Anna K Smith' && kept.body.nameConfirmed === true, kept.body);
check('the confirmed name stays on the next start', (await call('GET', '/api/profiles/me', anna)).body?.nameConfirmed === true);

const typedEmail = await call('PUT', '/api/profiles/me', ben, { displayName: 'benjamin.lind@example.com', bio: null, homeRegion: null, avatarUrl: null });
check('an email typed as the name is made readable', typedEmail.status === 200 && typedEmail.body?.displayName === 'Benjamin Lind', typedEmail.body);
const groupLast = await eventually(() => call('GET', `/api/groups/${group?.id}`, ben), (r) => nameOf(r.body?.members, ben) === 'Benjamin Lind');
check('and reaches the group as the readable name', nameOf(groupLast.body?.members, ben) === 'Benjamin Lind', groupLast.body?.members);

await call('DELETE', t, anna);
await call('DELETE', `/api/groups/${group?.id}`, anna);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll name checks passed');
process.exit(failed ? 1 : 0);
