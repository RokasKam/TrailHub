// Hike feed: sharing hikes with groups, who sees them (members, outsiders, people who leave), track privacy,
// photos, likes, comments, unsharing and deletion. Needs Groups and Activity in Auth:Mode=Development and the
// photo store running:
//   node scripts/hike-feed-test.mjs
// BASE defaults to the gateway.
const base = process.env.BASE ?? 'http://localhost:8080';
const call = (method, path, user, body, form) =>
  fetch(base + path, {
    method,
    headers: { Authorization: `Dev ${user}`, ...(form ? {} : { 'Content-Type': 'application/json' }) },
    body: form ?? (body && JSON.stringify(body)),
  }).then(async (r) => ({
    status: r.status,
    body: r.status === 204 ? null : await r.json().catch(() => null),
  }));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Group memberships reach Activity asynchronously over the bus; retry until they have. */
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
const problem = (res, status, code) => res.status === status && res.body?.code === code;

// Haversine distance in meters between [lat, lon] pairs.
const meters = ([lat1, lon1], [lat2, lon2]) => {
  const rad = Math.PI / 180;
  const a = Math.sin(((lat2 - lat1) * rad) / 2) ** 2 + Math.cos(lat1 * rad) * Math.cos(lat2 * rad) * Math.sin(((lon2 - lon1) * rad) / 2) ** 2;
  return 2 * 6371000 * Math.asin(Math.sqrt(a));
};

const photoForm = (type = 'image/png', size = 68, caption) => {
  const f = new FormData();
  f.append('file', new Blob([new Uint8Array(size)], { type }), type === 'image/png' ? 'photo.png' : 'photo.bin');
  if (caption) f.append('caption', caption);
  return f;
};

const stamp = Date.now();
const anna = `feed-a-${stamp}`, ben = `feed-b-${stamp}`, cleo = `feed-c-${stamp}`, xena = `feed-x-${stamp}`;

// Anna, Ben and Cleo hike together; Xena runs another group that Anna is also in.
const friends = (await call('POST', '/api/groups', anna, { name: 'Feed friends', displayName: 'Anna' })).body;
await call('POST', '/api/groups/join', ben, { inviteCode: friends.inviteCode, displayName: 'Ben' });
await call('POST', '/api/groups/join', cleo, { inviteCode: friends.inviteCode, displayName: 'Cleo' });
const club = (await call('POST', '/api/groups', xena, { name: 'Feed club', displayName: 'Xena' })).body;
await call('POST', '/api/groups/join', anna, { inviteCode: club.inviteCode, displayName: 'Anna K' });

// A 3 km hike due north, a point every ~55 m.
const id = crypto.randomUUID();
const t0 = Date.now() - 3 * 3600_000;
const points = Array.from({ length: 55 }, (_, i) => ({ latitude: 46 + i * 0.0005, longitude: 7.5, elevation: 1000 + i * 5, timestampUtc: new Date(t0 + i * 60_000).toISOString() }));
const start = [points[0].latitude, points[0].longitude];
const finish = [points.at(-1).latitude, points.at(-1).longitude];
check('a hike is recorded', (await call('PUT', `/api/activities/${id}`, anna, { name: 'Feed hike', segments: [points] })).status === 200);

const targets = await eventually(() => call('GET', '/api/activities/share-targets', anna), (r) => r.body?.length === 2 && r.body.some((g) => g.memberCount === 3));
check('share targets are my groups with member counts', targets.status === 200 && targets.body.some((g) => g.groupId === friends.id && g.name === 'Feed friends' && g.memberCount === 3) && targets.body.some((g) => g.groupId === club.id));
check('the hike is not shared yet', (await call('GET', `/api/activities/${id}`, anna)).body?.sharing === null);
check("unshared hikes aren't in the feed", (await call('GET', `/api/activities/feed/${id}`, ben)).status === 404);

// ── Sharing rules ──
check('sharing needs a group', problem(await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [], privacy: 'Full' }), 400, 'Activity.NoGroups'));
check("only groups I'm in", problem(await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [crypto.randomUUID()], privacy: 'Full' }), 400, 'Activity.NotAGroupMember'));
check('caption is limited', problem(await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [friends.id], caption: 'x'.repeat(501), privacy: 'Full' }), 400, 'Activity.InvalidCaption'));
check("others can't share my hike", problem(await call('PUT', `/api/activities/${id}/sharing`, ben, { groupIds: [friends.id], privacy: 'Full' }), 404, 'Activity.NotFound'));

const shared = await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [friends.id], caption: ' Lovely ridge ', privacy: 'HideEnds' });
check('sharing returns groups, caption and privacy', shared.status === 200 && shared.body.groupIds.length === 1 && shared.body.caption === 'Lovely ridge' && shared.body.privacy === 'HideEnds' && !!shared.body.sharedAtUtc);
const details = (await call('GET', `/api/activities/${id}`, anna)).body;
check('the hike page shows its sharing', details.sharing?.groupIds?.[0] === friends.id && Array.isArray(details.photos));

// ── Visibility and privacy ──
const benFeed = await call('GET', '/api/activities/feed', ben);
const benPost = benFeed.body?.items?.find((p) => p.activityId === id);
check('members see the post in their feed', benFeed.status === 200 && !!benPost && benFeed.body.totalCount >= 1);
check('the post names the author as the group knows them', benPost?.author?.userId === anna && benPost.author.displayName === 'Anna' && !benPost.isMine);
check('HideEnds drops the track near the start and finish', benPost?.track.length > 0 && benPost.track.every((p) => meters(p, start) >= 199 && meters(p, finish) >= 199));
check('only groups the viewer is in are listed', benPost?.groups.length === 1 && benPost.groups[0].groupId === friends.id && benPost.groups[0].name === 'Feed friends');
const annaPost = (await call('GET', `/api/activities/feed/${id}`, anna)).body;
check('the author sees their full track', annaPost?.isMine && meters(annaPost.track[0], start) < 1 && meters(annaPost.track.at(-1), finish) < 1);
check("outsiders don't see it", !(await call('GET', '/api/activities/feed', xena)).body?.items?.some((p) => p.activityId === id));
check('outsiders get 404 for the post', problem(await call('GET', `/api/activities/feed/${id}`, xena), 404, 'Activity.NotFound'));
check('the feed can be filtered to one of my groups', (await call('GET', `/api/activities/feed?groupId=${friends.id}`, ben)).body?.items?.some((p) => p.activityId === id));
check("filtering to a group I'm not in is 404", problem(await call('GET', `/api/activities/feed?groupId=${club.id}`, ben), 404, 'Activity.GroupNotFound'));
check('the feed pages', (await call('GET', '/api/activities/feed?page=1&pageSize=1', anna)).body?.items?.length === 1);

const reshared = await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [friends.id, club.id], privacy: 'StatsOnly' });
check('re-sharing keeps the original share time', reshared.status === 200 && reshared.body.sharedAtUtc === shared.body.sharedAtUtc && reshared.body.caption === null);
const xenaPost = (await call('GET', `/api/activities/feed/${id}`, xena)).body;
check('members of a newly added group see it', xenaPost?.activityId === id && xenaPost.groups.length === 1 && xenaPost.groups[0].groupId === club.id);
check('StatsOnly has no track or elevation profile', xenaPost?.privacy === 'StatsOnly' && xenaPost.track.length === 0 && xenaPost.elevationProfile.length === 0 && xenaPost.distanceMeters > 0);
check('the author still sees the full track', (await call('GET', `/api/activities/feed/${id}`, anna)).body?.track.length > 0);
await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [friends.id], privacy: 'Full' });

// ── Photos ──
const photo = await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm('image/png', 68, 'Summit'));
check('a photo can be added', photo.status === 201 && photo.body.url.includes('hikes/') && photo.body.caption === 'Summit' && !!photo.body.addedAtUtc);
check('unsupported types are rejected', problem(await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm('text/plain')), 400, 'Activity.UnsupportedPhoto'));
check('photos over 10 MB are rejected', problem(await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm('image/jpeg', 10 * 1024 * 1024 + 1)), 400, 'Activity.PhotoTooLarge'));
check("others can't add photos", (await call('POST', `/api/activities/${id}/photos`, ben, null, photoForm())).status === 404);
for (let i = 0; i < 5; i++) await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm('image/webp'));
check('at most 6 photos', problem(await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm()), 400, 'Activity.TooManyPhotos'));
const stored = await fetch(photo.body.url).then((r) => r.status).catch(() => 0);
check('the photo is served from the photo store', stored === 200);
check('members see the photos', (await call('GET', `/api/activities/feed/${id}`, ben)).body?.photos?.length === 6);
check('a photo can be removed', (await call('DELETE', `/api/activities/${id}/photos/${photo.body.id}`, anna)).status === 204);
check('removing a missing photo is 404', (await call('DELETE', `/api/activities/${id}/photos/${photo.body.id}`, anna)).status === 404);
check('the hike page lists the photos', (await call('GET', `/api/activities/${id}`, anna)).body?.photos?.length === 5);

// ── Likes ──
const liked = await call('PUT', `/api/activities/feed/${id}/like`, ben);
check('members can like', liked.status === 200 && liked.body.likeCount === 1 && liked.body.likedByMe === true);
check('liking is idempotent', (await call('PUT', `/api/activities/feed/${id}/like`, ben)).body?.likeCount === 1);
check('likes add up', (await call('PUT', `/api/activities/feed/${id}/like`, cleo)).body?.likeCount === 2);
const unliked = await call('DELETE', `/api/activities/feed/${id}/like`, ben);
check('unliking', unliked.status === 200 && unliked.body.likeCount === 1 && unliked.body.likedByMe === false);
check("outsiders can't like", (await call('PUT', `/api/activities/feed/${id}/like`, xena)).status === 404);

// ── Comments ──
const benComment = await call('POST', `/api/activities/feed/${id}/comments`, ben, { text: '  Looks great!  ' });
check('members can comment', benComment.status === 201 && benComment.body.text === 'Looks great!' && benComment.body.author.displayName === 'Ben' && benComment.body.canDelete);
check('blank comments are rejected', problem(await call('POST', `/api/activities/feed/${id}/comments`, ben, { text: '   ' }), 400, 'Activity.InvalidComment'));
check('long comments are rejected', problem(await call('POST', `/api/activities/feed/${id}/comments`, ben, { text: 'x'.repeat(1001) }), 400, 'Activity.InvalidComment'));
const cleoComment = (await call('POST', `/api/activities/feed/${id}/comments`, cleo, { text: 'Next time I am coming' })).body;
const annaComment = (await call('POST', `/api/activities/feed/${id}/comments`, anna, { text: 'Thanks!' })).body;
const asBen = (await call('GET', `/api/activities/feed/${id}`, ben)).body;
check('comments are listed oldest first', asBen?.comments?.map((c) => c.id).join() === [benComment.body.id, cleoComment.id, annaComment.id].join() && asBen.commentCount === 3);
check('canDelete only on my own comments', asBen?.comments?.filter((c) => c.canDelete).map((c) => c.id).join() === benComment.body.id);
check('the author can delete every comment', (await call('GET', `/api/activities/feed/${id}`, anna)).body?.comments?.every((c) => c.canDelete));
check("members can't delete others' comments", problem(await call('DELETE', `/api/activities/feed/${id}/comments/${cleoComment.id}`, ben), 403, 'Activity.NotCommentAuthor'));
check('commenters delete their own', (await call('DELETE', `/api/activities/feed/${id}/comments/${benComment.body.id}`, ben)).status === 204);
check('the author deletes any comment', (await call('DELETE', `/api/activities/feed/${id}/comments/${cleoComment.id}`, anna)).status === 204);
check('the feed shows like and comment counts', (await call('GET', '/api/activities/feed', ben)).body?.items?.some((p) => p.activityId === id && p.likeCount === 1 && p.commentCount === 1 && !p.likedByMe));

// ── Leaving the group ──
check('a member leaves the group', (await call('POST', `/api/groups/${friends.id}/leave`, cleo)).status === 204);
const gone = await eventually(() => call('GET', `/api/activities/feed/${id}`, cleo), (r) => r.status === 404);
check("people who leave stop seeing the group's posts", gone.status === 404 && !(await call('GET', '/api/activities/feed', cleo)).body?.items?.some((p) => p.activityId === id));

// ── Unsharing keeps likes and comments ──
check('unsharing', (await call('DELETE', `/api/activities/${id}/sharing`, anna)).status === 204);
check('unshared posts disappear for members', (await call('GET', `/api/activities/feed/${id}`, ben)).status === 404);
check('the hike page shows it unshared', (await call('GET', `/api/activities/${id}`, anna)).body?.sharing === null);
await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [friends.id], privacy: 'Full' });
const back = (await call('GET', `/api/activities/feed/${id}`, ben)).body;
check('sharing again brings likes and comments back', back?.likeCount === 1 && back.commentCount === 1);

// ── Deleting the hike ──
check('the hike is deleted', (await call('DELETE', `/api/activities/${id}`, anna)).status === 204);
check('deleted hikes leave the feed', (await call('GET', `/api/activities/feed/${id}`, ben)).status === 404 && !(await call('GET', '/api/activities/feed', ben)).body?.items?.some((p) => p.activityId === id));

await call('DELETE', `/api/groups/${friends.id}`, anna);
await call('DELETE', `/api/groups/${club.id}`, xena);

console.log(failed ? `\n${failed} check(s) failed` : '\nAll hike feed checks passed');
process.exit(failed ? 1 : 0);
