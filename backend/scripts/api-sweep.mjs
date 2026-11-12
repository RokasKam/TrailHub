// Calls every endpoint of every service through the gateway at least once, checks status codes, key response
// fields and the problem-details shape of errors, then lists any operation in the services' OpenAPI documents
// it didn't call. Needs the stack in Auth:Mode=Development with MODERATOR_USER_ID=moderation-mod and a Mapbox token:
//   node scripts/api-sweep.mjs
// BASE defaults to the gateway.
import { readFile } from 'node:fs/promises';

const base = process.env.BASE ?? 'http://localhost:8080';
const moderator = process.env.MODERATOR ?? 'moderation-mod';
const stamp = Date.now();
const a = `sweep-a-${stamp}`, b = `sweep-b-${stamp}`, outsider = `sweep-x-${stamp}`;

const called = new Set();
let failed = 0, checks = 0;
const check = (name, ok, detail) => {
  checks++;
  if (!ok) {
    failed++;
    console.log(`  ✗ ${name}${detail ? ` → ${detail}` : ''}`);
  }
};

/** Calls the API and records which operation (method + path template) was exercised. */
async function call(method, template, params, user, body, { form, raw } = {}) {
  const path = template.replace(/\{(\w+)\}/g, (_, k) => encodeURIComponent(params[k]));
  called.add(`${method} ${template.split('?')[0]}`);
  const headers = { Authorization: `Dev ${user}` };
  if (body && !form) headers['Content-Type'] = 'application/json';
  const res = await fetch(base + path, { method, headers, body: form ?? (body && JSON.stringify(body)) });
  const text = await res.text();
  let json = null;
  if (!raw) try { json = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
  return { status: res.status, body: json, text, type: res.headers.get('content-type') ?? '' };
}

/** Expects a status; on mismatch prints the response so failures are easy to read. */
function expect(name, res, status, ok = () => true) {
  const good = res.status === status && ok(res.body ?? res.text);
  check(`${name} (${status})`, good, good ? '' : `${res.status} ${res.text.slice(0, 200)}`);
  return res.body;
}

/** Errors must stay RFC 9457 problem details: status, title = error code, detail = message, plus a `code` extension. */
function expectProblem(name, res, status, code) {
  const p = res.body;
  const ok = res.status === status && res.type.includes('json') && p?.status === status && typeof p?.detail === 'string'
    && (code ? p.title === code && p.code === code : true);
  check(`${name} (${status}${code ? ` ${code}` : ''})`, ok, ok ? '' : `${res.status} ${res.type} ${res.text.slice(0, 200)}`);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function eventually(fn, ok, ms = 15000) {
  const until = Date.now() + ms;
  for (;;) {
    const r = await fn();
    if (ok(r) || Date.now() > until) return r;
    await sleep(300);
  }
}
const missing = '00000000-0000-0000-0000-000000000001';
const gpx = await readFile(new URL('../samples/demo-loop.gpx', import.meta.url));
const gpxForm = (fields) => {
  const f = new FormData();
  f.append('file', new Blob([gpx]), 'demo.gpx');
  for (const [k, v] of Object.entries(fields)) f.append(k, v);
  return f;
};
const chamonix = { latitude: 45.9237, longitude: 6.8694, name: 'Chamonix' };
const argentiere = { latitude: 45.9668, longitude: 6.9285, name: 'Argentière' };

// ── Profile ────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Profile');
expect('get my profile', await call('GET', '/api/profiles/me', {}, a), 200, (p) => p.userId === a || !!p.displayName);
expect('update profile', await call('PUT', '/api/profiles/me', {}, a, { displayName: 'Sweep A', bio: 'API sweep', homeRegion: 'Alps' }), 200, (p) => p.displayName === 'Sweep A');
expectProblem('blank display name', await call('PUT', '/api/profiles/me', {}, a, { displayName: '' }), 400);
expect('update preferences', await call('PUT', '/api/profiles/me/preferences', {}, a, { units: 'Imperial', mapStyle: 'Terrain', shareLocationWithGroups: true, notificationsEnabled: false }), 200, (p) => p.preferences?.units === 'Imperial');
expect('achievements', await call('GET', '/api/profiles/me/achievements', {}, a), 200, Array.isArray);

// ── Weather ────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Weather');
expect('forecast', await call('GET', '/api/weather?lat=45.92&lon=6.87&elevation=1500', {}, a), 200, (f) => Array.isArray(f.daily) && f.daily.length > 0);
expectProblem('latitude out of range', await call('GET', '/api/weather?lat=95&lon=6.87', {}, a), 400);

// ── Trails ─────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Trails');
const imported = expect('import GPX', await call('POST', '/api/trails/import', {}, a, null, { form: gpxForm({ name: `Sweep trail ${stamp}`, region: 'Tatras', difficulty: 'Moderate' }) }), 201, (t) => !!t.id);
const trail = imported?.id;
expect('search by text', await call('GET', `/api/trails?q=${encodeURIComponent(`Sweep trail ${stamp}`)}`, {}, b), 200, (r) => r.items.some((t) => t.id === trail));
expect('search by radius', await call('GET', '/api/trails?lat=49.26&lon=19.96&radiusKm=20&sort=Distance', {}, b), 200, (r) => Array.isArray(r.items));
expectProblem('radius over the limit', await call('GET', '/api/trails?lat=49.26&lon=19.96&radiusKm=5000', {}, b), 400);
expect('details', await call('GET', '/api/trails/{id}', { id: trail }, b), 200, (t) => t.id === trail && t.name === `Sweep trail ${stamp}`);
expectProblem('missing trail', await call('GET', '/api/trails/{id}', { id: missing }, b), 404, 'Trail.NotFound');
expect('update (author)', await call('PUT', '/api/trails/{id}', { id: trail }, a, { name: `Sweep trail ${stamp}`, description: 'Updated', region: 'Tatras', difficulty: 'Hard' }), 204);
expectProblem('update (not the author)', await call('PUT', '/api/trails/{id}', { id: trail }, b, { name: 'x', region: 'y', difficulty: 'Easy' }), 403);
expect('route', await call('GET', '/api/trails/{id}/route?maxPoints=50', { id: trail }, b), 200, (r) => r.points.length > 1);
expect('elevation profile', await call('GET', '/api/trails/{id}/elevation-profile?samples=20', { id: trail }, b), 200, (r) => Array.isArray(r) || Array.isArray(r.samples));
const trailGpx = await call('GET', '/api/trails/{id}/gpx', { id: trail }, b, null, { raw: true });
check('trail GPX export (200)', trailGpx.status === 200 && trailGpx.text.includes('<gpx'), `${trailGpx.status}`);
const poi = expect('add point of interest', await call('POST', '/api/trails/{id}/points-of-interest', { id: trail }, a, { name: 'Spring', type: 'WaterSource', latitude: 49.25, longitude: 19.95 }), 201, (p) => !!p.id);
expectProblem('remove POI (not the author)', await call('DELETE', '/api/trails/{id}/points-of-interest/{poiId}', { id: trail, poiId: poi?.id }, b), 403);
expect('remove POI', await call('DELETE', '/api/trails/{id}/points-of-interest/{poiId}', { id: trail, poiId: poi?.id }, a), 204);
expect('favorite', await call('PUT', '/api/trails/{id}/favorite', { id: trail }, b), 204);
expect('favorites list', await call('GET', '/api/trails/favorites', {}, b), 200, (r) => (r.items ?? r).some((t) => t.id === trail));
expect('unfavorite', await call('DELETE', '/api/trails/{id}/favorite', { id: trail }, b), 204);
expect('review', await call('PUT', '/api/trails/{id}/reviews/mine', { id: trail }, b, { rating: 4, comment: 'Nice' }), 200, (r) => r.rating === 4);
expectProblem('review rating 9', await call('PUT', '/api/trails/{id}/reviews/mine', { id: trail }, b, { rating: 9 }), 400);
expect('reviews', await call('GET', '/api/trails/{id}/reviews', { id: trail }, a), 200, (r) => r.items.length === 1);
expect('condition report', await call('POST', '/api/trails/{id}/conditions', { id: trail }, b, { condition: 'Muddy', note: 'After rain' }), 201);
expect('conditions', await call('GET', '/api/trails/{id}/conditions', { id: trail }, a), 200, (r) => r.length === 1 && r[0].condition === 'Muddy');
const png = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='), (c) => c.charCodeAt(0));
const photoForm = new FormData();
photoForm.append('file', new Blob([png], { type: 'image/png' }), 'dot.png');
photoForm.append('caption', 'Dot');
const trailPhoto = expect('upload photo', await call('POST', '/api/trails/{id}/photos/upload', { id: trail }, b, null, { form: photoForm }), 201, (p) => !!p.url && !!p.id && p.uploadedBy === b && p.canDelete);
expect('attach photo by URL', await call('POST', '/api/trails/{id}/photos', { id: trail }, b, { url: 'https://example.com/photo.jpg', caption: 'Link' }), 204);
expectProblem('attach photo with a bad URL', await call('POST', '/api/trails/{id}/photos', { id: trail }, b, { url: 'not a url' }), 400);
expect('details list photos with ids', await call('GET', '/api/trails/{id}', { id: trail }, a), 200, (t) => t.photos.some((p) => p.id === trailPhoto?.id && p.canDelete && !p.hidden));
expectProblem('report own photo', await call('POST', '/api/trails/{id}/photos/{photoId}/report', { id: trail, photoId: trailPhoto?.id }, b, { reason: 'Spam' }), 400, 'Trail.CantReportOwnContent');
expect('report photo', await call('POST', '/api/trails/{id}/photos/{photoId}/report', { id: trail, photoId: trailPhoto?.id }, a, { reason: 'Inappropriate', details: 'Sweep' }), 204);
expectProblem('photo queue (not a moderator)', await call('GET', '/api/trails/moderation/photos', {}, b), 403, 'Trail.NotModerator');
expect('photo queue (moderator)', await call('GET', '/api/trails/moderation/photos', {}, moderator), 200, (q) => q.some((x) => x.photoId === trailPhoto?.id && x.openReports === 1 && x.reports[0].userId === a));
expectProblem('moderate photo (not a moderator)', await call('PUT', '/api/trails/{id}/photos/{photoId}/moderation', { id: trail, photoId: trailPhoto?.id }, b, { action: 'Hide' }), 403, 'Trail.NotModerator');
expect('restore photo (moderator)', await call('PUT', '/api/trails/{id}/photos/{photoId}/moderation', { id: trail, photoId: trailPhoto?.id }, moderator, { action: 'Restore' }), 204);
expectProblem('delete photo (not the uploader)', await call('DELETE', '/api/trails/{id}/photos/{photoId}', { id: trail, photoId: trailPhoto?.id }, outsider), 403, 'Trail.NotPhotoOwner');
expect('delete photo (uploader)', await call('DELETE', '/api/trails/{id}/photos/{photoId}', { id: trail, photoId: trailPhoto?.id }, b), 204);
expectProblem('delete missing photo', await call('DELETE', '/api/trails/{id}/photos/{photoId}', { id: trail, photoId: trailPhoto?.id }, a), 404, 'Trail.PhotoNotFound');

// ── Moderation ─────────────────────────────────────────────────────────────────────────────────────────────
console.log('Moderation');
expectProblem("report own trail", await call('POST', '/api/trails/{id}/report', { id: trail }, a, { reason: 'Spam' }), 400);
expect('report', await call('POST', '/api/trails/{id}/report', { id: trail }, b, { reason: 'Inaccurate', details: 'Wrong start' }), 204);
expectProblem('queue (not a moderator)', await call('GET', '/api/trails/moderation', {}, b), 403);
expect('queue (moderator)', await call('GET', '/api/trails/moderation', {}, moderator), 200, (q) => q.some((x) => x.trailId === trail));
expectProblem('hide (not a moderator)', await call('PUT', '/api/trails/{id}/moderation', { id: trail }, b, { hidden: true }), 403);
expect('hide (moderator)', await call('PUT', '/api/trails/{id}/moderation', { id: trail }, moderator, { hidden: true }), 204);
expectProblem('hidden trail for others', await call('GET', '/api/trails/{id}', { id: trail }, b), 404);

// ── Activity ───────────────────────────────────────────────────────────────────────────────────────────────
console.log('Activity');
const activityId = crypto.randomUUID();
const t0 = Date.now() - 3600_000;
const points = [0, 1, 2, 3].map((i) => ({ latitude: 49.25 + i * 0.001, longitude: 19.95, elevation: 1000 + i * 10, timestampUtc: new Date(t0 + i * 60_000).toISOString() }));
expect('record', await call('PUT', '/api/activities/{id}', { id: activityId }, a, { name: 'Sweep hike', segments: [points] }), 200, (h) => h.id === activityId && h.distanceMeters > 0);
expect('record again (idempotent)', await call('PUT', '/api/activities/{id}', { id: activityId }, a, { name: 'Sweep hike', segments: [points] }), 200, (h) => h.id === activityId);
expectProblem('same id, other user', await call('PUT', '/api/activities/{id}', { id: activityId }, b, { name: 'x', segments: [points] }), 409, 'Activity.IdTaken');
expect('list', await call('GET', '/api/activities?page=1&pageSize=5', {}, a), 200, (r) => r.items.some((h) => h.id === activityId));
expect('details', await call('GET', '/api/activities/{id}', { id: activityId }, a), 200, (h) => h.segments.length === 1 && h.elevationProfile.length > 0);
expectProblem("someone else's hike", await call('GET', '/api/activities/{id}', { id: activityId }, b), 404, 'Activity.NotFound');
expect('rename', await call('PATCH', '/api/activities/{id}', { id: activityId }, a, { name: 'Renamed', notes: 'ok' }), 204);
expectProblem('rename to blank', await call('PATCH', '/api/activities/{id}', { id: activityId }, a, { name: '' }), 400);
const actGpx = await call('GET', '/api/activities/{id}/gpx', { id: activityId }, a, null, { raw: true });
check('activity GPX export (200)', actGpx.status === 200 && actGpx.text.includes('<trkseg'), `${actGpx.status}`);
expect('statistics', await call('GET', '/api/activities/statistics', {}, a), 200, (s) => s.allTime.hikes >= 1 && s.lastTwelveMonths.length === 12);
expectProblem('import GPX without timestamps', await call('POST', '/api/activities/import', {}, a, null, { form: gpxForm({ name: 'No times' }) }), 400, 'Activity.GpxWithoutTimestamps');
const hikeForm = new FormData();
hikeForm.append('file', new Blob([actGpx.text]), 'hike.gpx');
hikeForm.append('name', 'Imported sweep');
const importedHike = expect('import GPX', await call('POST', '/api/activities/import', {}, a, null, { form: hikeForm }), 201, (h) => !!h.id);
expect('delete imported', await call('DELETE', '/api/activities/{id}', { id: importedHike?.id }, a), 204);
expectProblem('delete missing', await call('DELETE', '/api/activities/{id}', { id: missing }, a), 404);

// ── Groups ─────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Groups');
const group = expect('create', await call('POST', '/api/groups', {}, a, { name: 'Sweep group', description: 'API sweep', displayName: 'Anna' }), 201, (g) => !!g.inviteCode);
expectProblem('create with blank name', await call('POST', '/api/groups', {}, a, { name: '', displayName: 'Anna' }), 400);
expectProblem('join with a wrong code', await call('POST', '/api/groups/join', {}, b, { inviteCode: 'NOPE0000', displayName: 'Ben' }), 400);
expect('join', await call('POST', '/api/groups/join', {}, b, { inviteCode: group?.inviteCode, displayName: 'Ben' }), 200, (g) => g.members.length === 2);
expectProblem('join twice', await call('POST', '/api/groups/join', {}, b, { inviteCode: group?.inviteCode, displayName: 'Ben' }), 409);
await call('POST', '/api/groups/join', {}, outsider, { inviteCode: group?.inviteCode, displayName: 'Xena' });
expect('list mine', await call('GET', '/api/groups', {}, b), 200, (r) => r.some((g) => g.id === group?.id));
expect('details', await call('GET', '/api/groups/{id}', { id: group?.id }, b), 200, (g) => g.id === group?.id);
expectProblem('details (stranger)', await call('GET', '/api/groups/{id}', { id: group?.id }, `stranger-${stamp}`), 404);
expectProblem('new invite code (member)', await call('POST', '/api/groups/{id}/invite-code', { id: group?.id }, b), 403);
expect('new invite code (owner)', await call('POST', '/api/groups/{id}/invite-code', { id: group?.id }, a), 200, (g) => g.inviteCode && g.inviteCode !== group?.inviteCode);
const hike = expect('schedule hike', await call('POST', '/api/groups/{id}/hikes', { id: group?.id }, a, { title: 'Sunrise', trailId: null, scheduledAtUtc: new Date(Date.now() + 86400_000).toISOString(), meetingLatitude: 49.25, meetingLongitude: 19.95 }), 201, (h) => !!h.id);
expectProblem('schedule in the past', await call('POST', '/api/groups/{id}/hikes', { id: group?.id }, a, { title: 'Late', scheduledAtUtc: '2020-01-01T08:00:00Z' }), 400);
expect('RSVP', await call('PUT', '/api/groups/{id}/hikes/{hikeId}/rsvp', { id: group?.id, hikeId: hike?.id }, b, { status: 'Going' }), 200);
expectProblem('RSVP to a missing hike', await call('PUT', '/api/groups/{id}/hikes/{hikeId}/rsvp', { id: group?.id, hikeId: missing }, b, { status: 'Going' }), 404);
expectProblem('cancel hike (member)', await call('DELETE', '/api/groups/{id}/hikes/{hikeId}', { id: group?.id, hikeId: hike?.id }, b), 403);
expect('cancel hike (owner)', await call('DELETE', '/api/groups/{id}/hikes/{hikeId}', { id: group?.id, hikeId: hike?.id }, a), 204);
expect('share location', await call('POST', '/api/groups/{id}/locations/mine', { id: group?.id }, b, { latitude: 49.25, longitude: 19.95, accuracyMeters: 5 }), 200);
expect('live locations', await call('GET', '/api/groups/{id}/locations', { id: group?.id }, a), 200, (l) => l.some((x) => x.userId === b));
expect('stop sharing', await call('DELETE', '/api/groups/{id}/locations/mine', { id: group?.id }, b), 204);
expectProblem('remove member (not owner)', await call('DELETE', '/api/groups/{id}/members/{userId}', { id: group?.id, userId: outsider }, b), 403);
expect('remove member (owner)', await call('DELETE', '/api/groups/{id}/members/{userId}', { id: group?.id, userId: outsider }, a), 204);
expectProblem('owner leaves without handing over', await call('POST', '/api/groups/{id}/leave', { id: group?.id }, a), 409);
expectProblem('transfer ownership (member)', await call('POST', '/api/groups/{id}/owner', { id: group?.id }, b, { userId: b }), 403);

// ── Hike feed (Activity) ───────────────────────────────────────────────────────────────────────────────────
console.log('Hike feed');
const targets = await eventually(() => call('GET', '/api/activities/share-targets', {}, a), (r) => r.body?.some?.((g) => g.groupId === group?.id && g.memberCount === 2));
expect('share targets', targets, 200, (r) => r.some((g) => g.groupId === group?.id && g.name === 'Sweep group'));
const hp = { id: activityId };
expect('share hike', await call('PUT', '/api/activities/{id}/sharing', hp, a, { groupIds: [group?.id], caption: 'Sweep', privacy: 'HideEnds' }), 200, (r) => r.groupIds[0] === group?.id && r.privacy === 'HideEnds' && !!r.sharedAtUtc);
expectProblem('share with no groups', await call('PUT', '/api/activities/{id}/sharing', hp, a, { groupIds: [], privacy: 'Full' }), 400, 'Activity.NoGroups');
expectProblem("share with a group I'm not in", await call('PUT', '/api/activities/{id}/sharing', hp, a, { groupIds: [missing], privacy: 'Full' }), 400, 'Activity.NotAGroupMember');
expectProblem("share someone else's hike", await call('PUT', '/api/activities/{id}/sharing', hp, b, { groupIds: [group?.id], privacy: 'Full' }), 404, 'Activity.NotFound');
const hikePhotoForm = (type) => {
  const f = new FormData();
  f.append('file', new Blob([new Uint8Array(68)], { type }), 'photo');
  f.append('caption', 'Sweep photo');
  return f;
};
const hikePhoto = expect('add hike photo', await call('POST', '/api/activities/{id}/photos', hp, a, null, { form: hikePhotoForm('image/png') }), 201, (p) => p.url.includes('hikes/') && p.caption === 'Sweep photo');
expectProblem('add an unsupported hike photo', await call('POST', '/api/activities/{id}/photos', hp, a, null, { form: hikePhotoForm('text/plain') }), 400, 'Activity.UnsupportedPhoto');
expect('hike details show sharing and photos', await call('GET', '/api/activities/{id}', hp, a), 200, (h) => h.sharing?.groupIds?.length === 1 && h.photos.length === 1);
expect('feed', await call('GET', '/api/activities/feed?page=1&pageSize=10', {}, b), 200, (r) => r.items.some((p) => p.activityId === activityId && !p.isMine && p.author.displayName === 'Anna'));
expectProblem("feed for a group I'm not in", await call('GET', `/api/activities/feed?groupId=${missing}`, {}, b), 404, 'Activity.GroupNotFound');
expect('feed post', await call('GET', '/api/activities/feed/{id}', hp, b), 200, (p) => p.activityId === activityId && Array.isArray(p.comments) && Array.isArray(p.elevationProfile));
const hidden = await eventually(() => call('GET', '/api/activities/feed/{id}', hp, outsider), (r) => r.status === 404);
expectProblem('feed post (removed member)', hidden, 404, 'Activity.NotFound');
expect('like', await call('PUT', '/api/activities/feed/{id}/like', hp, b), 200, (r) => r.likeCount === 1 && r.likedByMe === true);
expect('unlike', await call('DELETE', '/api/activities/feed/{id}/like', hp, b), 200, (r) => r.likeCount === 0 && r.likedByMe === false);
const comment = expect('comment', await call('POST', '/api/activities/feed/{id}/comments', hp, a, { text: ' Nice one ' }), 201, (c) => c.text === 'Nice one' && c.canDelete);
expectProblem('blank comment', await call('POST', '/api/activities/feed/{id}/comments', hp, b, { text: ' ' }), 400, 'Activity.InvalidComment');
expectProblem("delete someone else's comment", await call('DELETE', '/api/activities/feed/{id}/comments/{commentId}', { ...hp, commentId: comment?.id }, b), 403, 'Activity.NotCommentAuthor');
expectProblem('report own comment', await call('POST', '/api/activities/feed/{id}/comments/{commentId}/report', { ...hp, commentId: comment?.id }, a, { reason: 'Spam' }), 400, 'Activity.CantReportOwnContent');
expect('report comment', await call('POST', '/api/activities/feed/{id}/comments/{commentId}/report', { ...hp, commentId: comment?.id }, b, { reason: 'Harassment' }), 204);
expect('report hike photo', await call('POST', '/api/activities/feed/{id}/photos/{photoId}/report', { ...hp, photoId: hikePhoto?.id }, b, { reason: 'Spam', details: 'Sweep' }), 204);
expectProblem('report a missing hike photo', await call('POST', '/api/activities/feed/{id}/photos/{photoId}/report', { ...hp, photoId: missing }, b, { reason: 'Spam' }), 404, 'Activity.PhotoNotFound');
expectProblem('content queue (not a moderator)', await call('GET', '/api/activities/moderation', {}, b), 403, 'Activity.NotModerator');
expect('content queue (moderator)', await call('GET', '/api/activities/moderation', {}, moderator), 200, (q) => q.some((x) => x.contentId === comment?.id && x.kind === 'Comment' && x.openReports === 1) && q.some((x) => x.contentId === hikePhoto?.id && x.kind === 'HikePhoto'));
expect('restore comment (moderator)', await call('PUT', '/api/activities/moderation/comments/{contentId}', { contentId: comment?.id }, moderator, { action: 'Restore' }), 204);
expectProblem('moderate comment (not a moderator)', await call('PUT', '/api/activities/moderation/comments/{contentId}', { contentId: comment?.id }, b, { action: 'Hide' }), 403, 'Activity.NotModerator');
expectProblem('moderate a missing comment', await call('PUT', '/api/activities/moderation/comments/{contentId}', { contentId: missing }, moderator, { action: 'Hide' }), 404, 'Activity.CommentNotFound');
expect('hide hike photo (moderator)', await call('PUT', '/api/activities/moderation/photos/{contentId}', { contentId: hikePhoto?.id }, moderator, { action: 'Hide' }), 204);
expect('hidden photo marked for the author', await call('GET', '/api/activities/{id}', hp, a), 200, (h) => h.photos.some((p) => p.id === hikePhoto?.id && p.hidden));
expectProblem('moderate a missing hike photo', await call('PUT', '/api/activities/moderation/photos/{contentId}', { contentId: missing }, moderator, { action: 'Hide' }), 404, 'Activity.PhotoNotFound');
expect('delete comment', await call('DELETE', '/api/activities/feed/{id}/comments/{commentId}', { ...hp, commentId: comment?.id }, a), 204);
expect('delete hike photo', await call('DELETE', '/api/activities/{id}/photos/{photoId}', { ...hp, photoId: hikePhoto?.id }, a), 204);
expect('unshare hike', await call('DELETE', '/api/activities/{id}/sharing', hp, a), 204);
expectProblem('unshared post', await call('GET', '/api/activities/feed/{id}', hp, b), 404, 'Activity.NotFound');

// ── Routes ─────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Routes');
expect('preview', await call('POST', '/api/routes/preview', {}, a, { waypoints: [chamonix, argentiere] }), 200, (r) => r.stats?.distanceMeters > 1000);
expectProblem('preview with one point', await call('POST', '/api/routes/preview', {}, a, { waypoints: [chamonix] }), 400);
expect('alternatives', await call('POST', '/api/routes/alternatives', {}, a, { from: chamonix, to: argentiere }), 200, (r) => Array.isArray(r) && r.length >= 1);
expect('plan itinerary', await call('POST', '/api/routes/plan', {}, a, { start: chamonix, finish: argentiere, days: 1, accommodation: 'Hut', difficulty: 'Moderate' }), 200, (r) => r.days?.length >= 1);
expect('POIs along a line', await call('POST', '/api/routes/pois', {}, a, { line: [[6.8694, 45.9237], [6.9285, 45.9668]], kinds: ['MountainHut', 'Viewpoint'], corridorMeters: 1000 }), 200, Array.isArray);
expect('place search', await call('GET', '/api/routes/places?q=Chamonix&lat=45.9&lon=6.9', {}, a), 200, (r) => Array.isArray(r) && r.length > 0);
expect('suggest name', await call('POST', '/api/routes/suggest-name', {}, a, { waypoints: [chamonix, argentiere] }), 200, (r) => typeof r.name === 'string');
const route = expect('save', await call('POST', '/api/routes', {}, a, { name: 'Sweep route', waypoints: [chamonix, argentiere] }), 201, (r) => !!r.id && r.isOwner);
expectProblem('save with no waypoints', await call('POST', '/api/routes', {}, a, { name: 'x', waypoints: [] }), 400);
expect('list', await call('GET', '/api/routes', {}, a), 200, (r) => r.some((x) => x.id === route?.id));
expect('get', await call('GET', '/api/routes/{id}', { id: route?.id }, a), 200, (r) => r.id === route?.id);
expectProblem('private route for others', await call('GET', '/api/routes/{id}', { id: route?.id }, b), 404);
expect('update', await call('PUT', '/api/routes/{id}', { id: route?.id }, a, { name: 'Sweep route 2', waypoints: [chamonix, argentiere] }), 200, (r) => r.name === 'Sweep route 2');
const routeGpx = await call('GET', '/api/routes/{id}/gpx?pois=false', { id: route?.id }, a, null, { raw: true });
check('route GPX export (200)', routeGpx.status === 200 && routeGpx.text.includes('<gpx'), `${routeGpx.status}`);
const shared = await eventually(() => call('PUT', '/api/routes/{id}/groups/{groupId}', { id: route?.id, groupId: group?.id }, a), (x) => x.status === 200);
expect('share with group', shared, 200, (r) => r.sharedWith?.some((g) => g.id === group?.id));
expect('member sees it', await call('GET', '/api/routes/{id}', { id: route?.id }, b), 200, (r) => r.isOwner === false);
const copy = expect('member copies it', await call('POST', '/api/routes/{id}/copy', { id: route?.id }, b), 201, (r) => r.isOwner && r.id !== route?.id);
expectProblem('publish (member, not the owner)', await call('POST', '/api/routes/{id}/publish', { id: route?.id }, b, { name: 'x', region: 'y', difficulty: 'Easy' }), 403);
expect('unshare', await call('DELETE', '/api/routes/{id}/groups/{groupId}', { id: route?.id, groupId: group?.id }, a), 200, (r) => !r.sharedWith?.length);
expect('publish', await call('POST', '/api/routes/{id}/publish', { id: route?.id }, a, { name: `Sweep published ${stamp}`, region: 'Chamonix', difficulty: 'Moderate' }), 200, (r) => !!r.publishedAtUtc);
const pubTrail = await eventually(() => call('GET', '/api/trails/{id}', { id: route?.id }, b), (x) => x.status === 200);
check('published route appears as a trail (200)', pubTrail.status === 200, `${pubTrail.status}`);
expect('unpublish', await call('POST', '/api/routes/{id}/unpublish', { id: route?.id }, a), 200, (r) => !r.publishedAtUtc);

// ── Trips ──────────────────────────────────────────────────────────────────────────────────────────────────
console.log('Trips');
const trip = expect('create', await call('POST', '/api/trips', {}, a, { name: 'Sweep trip', description: 'API sweep', startDate: '2027-07-01', endDate: '2027-07-03' }), 201, (t) => t.days.length === 3);
expectProblem('create with end before start', await call('POST', '/api/trips', {}, a, { name: 'x', startDate: '2027-07-03', endDate: '2027-07-01' }), 400);
const tp = { id: trip?.id };
expect('list', await call('GET', '/api/trips', {}, a), 200, (r) => r.some((t) => t.id === trip?.id));
expect('get', await call('GET', '/api/trips/{id}', tp, a), 200, (t) => t.id === trip?.id);
expectProblem("someone else's trip", await call('GET', '/api/trips/{id}', tp, outsider), 404, 'Trip.NotFound');
expect('update details', await call('PUT', '/api/trips/{id}', tp, a, { name: 'Sweep trip 2', description: 'Renamed' }), 200, (t) => t.name === 'Sweep trip 2');
expect('reschedule', await call('PUT', '/api/trips/{id}/dates', tp, a, { startDate: '2027-07-02', endDate: '2027-07-05' }), 200, (t) => t.days.length === 4);
expect('day notes', await call('PUT', '/api/trips/{id}/days/{day}', { ...tp, day: 2 }, a, { notes: 'Long day', overnight: 'Refuge du Lac Blanc' }), 200, (t) => t.days[1].overnight === 'Refuge du Lac Blanc');
expect('add trail to day', await call('PUT', '/api/trips/{id}/days/{day}/trails/{trailId}', { ...tp, day: 1, trailId: route?.id }, a), 200, (t) => t.days[0].trailIds?.includes(route?.id) || JSON.stringify(t.days[0]).includes(route?.id));
expectProblem('add the same trail twice', await call('PUT', '/api/trips/{id}/days/{day}/trails/{trailId}', { ...tp, day: 1, trailId: route?.id }, a), 409);
expect('remove trail from day', await call('DELETE', '/api/trips/{id}/days/{day}/trails/{trailId}', { ...tp, day: 1, trailId: route?.id }, a), 200);
expect('attach route', await call('PUT', '/api/trips/{id}/route', tp, a, { routeId: route?.id }), 200, (t) => t.routeId === route?.id);
expect('share with group', await call('PUT', '/api/trips/{id}/group', tp, a, { groupId: group?.id }), 200, (t) => t.groupId === group?.id);
const withItem = expect('add checklist item', await call('POST', '/api/trips/{id}/checklist', tp, a, { text: 'Tent', kind: 'Shared' }), 200, (t) => t.checklist.some((i) => i.text === 'Tent'));
const item = withItem?.checklist?.find((i) => i.text === 'Tent');
expect('bulk add', await call('POST', '/api/trips/{id}/checklist/bulk', tp, b, { items: [{ text: 'Stove', kind: 'Shared' }, { text: 'Socks', kind: 'Personal' }] }), 200, (t) => t.checklist.length >= 3);
expect('member claims group item', await call('PUT', '/api/trips/{id}/checklist/{itemId}/claim', { ...tp, itemId: item?.id }, b, { claim: true }), 200);
expectProblem('second claim', await call('PUT', '/api/trips/{id}/checklist/{itemId}/claim', { ...tp, itemId: item?.id }, a, { claim: true }), 409);
expect('mark done', await call('PUT', '/api/trips/{id}/checklist/{itemId}', { ...tp, itemId: item?.id }, b, { isDone: true }), 200);
const gearList = expect('create gear list', await call('POST', '/api/trips/gear-lists', {}, a, { name: 'Sweep list', items: [{ text: 'Map', kind: 'Personal' }] }), 200, (l) => !!l.id);
expect('list gear lists', await call('GET', '/api/trips/gear-lists', {}, a), 200, (r) => r.some((l) => l.id === gearList?.id) && r.some((l) => l.isStarter));
expect('update gear list', await call('PUT', '/api/trips/gear-lists/{id}', { id: gearList?.id }, a, { name: 'Sweep list 2', items: [{ text: 'Map', kind: 'Personal' }, { text: 'Compass', kind: 'Personal' }] }), 200, (l) => l.items.length === 2);
expectProblem("update someone else's list", await call('PUT', '/api/trips/gear-lists/{id}', { id: gearList?.id }, b, { name: 'x', items: [] }), 404);
expect('add gear list to trip', await call('POST', '/api/trips/{id}/checklist/from-list/{listId}', { ...tp, listId: gearList?.id }, a), 200, (r) => JSON.stringify(r).includes('Compass'));
expectProblem('add a missing list', await call('POST', '/api/trips/{id}/checklist/from-list/{listId}', { ...tp, listId: missing }, a), 404);
const savedList = expect('save checklist as list', await call('POST', '/api/trips/{id}/checklist/save-as-list', tp, a, { name: 'From sweep trip' }), 200, (l) => !!l.id);
expect('gear suggestions', await call('POST', '/api/trips/{id}/gear-suggestions', tp, a, { hutNights: 2, maxElevationMeters: 2900, longestDayMinutes: 400 }), 200, (s) => Array.isArray(s) && s.length > 0);
expect('remove checklist item', await call('DELETE', '/api/trips/{id}/checklist/{itemId}', { ...tp, itemId: item?.id }, a), 200);
expect('trips for group', await call('GET', `/api/trips?groupId=${group?.id}`, {}, b), 200, (r) => r.some((t) => t.id === trip?.id));
expect('set trip currency', await call('PUT', '/api/trips/{id}/currency', tp, a, { currency: 'CHF' }), 200, (t) => t.currency === 'CHF' && t.expenseSummary?.expenseCount === 0);
expect('currencies', await call('GET', '/api/trips/currencies', {}, a), 200, (r) => r.some((c) => c.code === 'EUR'));
const fx = await call('GET', '/api/trips/exchange-rate?from=EUR&to=CHF&date=2026-06-30', {}, a);
check(`exchange rate (200 or 503 offline)`, (fx.status === 200 && fx.body?.rate > 0) || (fx.status === 503 && fx.body?.code === 'Trip.RatesUnavailable'), `${fx.status} ${fx.text.slice(0, 200)}`);
expectProblem('exchange rate with a bad code', await call('GET', '/api/trips/exchange-rate?from=XX&to=CHF', {}, a), 400, 'Trip.InvalidCurrency');
const expenseBody = { description: 'Hut dinner', category: 'Food', paidBy: a, amount: 90, currency: 'CHF', date: '2027-07-02', splitMode: 'Equal', participants: [a, b] };
const expense = expect('add expense', await call('POST', '/api/trips/{id}/expenses', tp, a, expenseBody), 201, (e) => e.shares.length === 2 && e.amountInTripCurrency === 90);
expectProblem('expense in another currency without a rate', await call('POST', '/api/trips/{id}/expenses', tp, a, { ...expenseBody, currency: 'EUR' }), 400, 'Trip.RateRequired');
expectProblem('currency locked', await call('PUT', '/api/trips/{id}/currency', tp, a, { currency: 'EUR' }), 409, 'Trip.CurrencyLocked');
expect('update expense', await call('PUT', '/api/trips/{id}/expenses/{expenseId}', { ...tp, expenseId: expense?.id }, a, { ...expenseBody, currency: 'EUR', rate: 0.94, splitMode: 'Custom', customShares: [{ userId: a, amount: 30 }, { userId: b, amount: 60 }] }), 200, (e) => e.amountInTripCurrency === 84.6);
expectProblem("member edits someone else's expense", await call('PUT', '/api/trips/{id}/expenses/{expenseId}', { ...tp, expenseId: expense?.id }, b, expenseBody), 403, 'Trip.NotExpenseEditor');
const payment = expect('record payment', await call('POST', '/api/trips/{id}/payments', tp, b, { from: b, to: a, amount: 56.4, date: '2027-07-03' }), 201, (p) => p.from.userId === b && p.amount === 56.4);
expect('expenses and balances', await call('GET', '/api/trips/{id}/expenses', tp, b), 200, (r) => r.currency === 'CHF' && r.balances.every((x) => x.net === 0) && r.suggestions.length === 0 && r.payments.length === 1);
expectProblem('expenses of a trip you can\'t see', await call('GET', '/api/trips/{id}/expenses', tp, outsider), 404, 'Trip.NotFound');
expect('delete payment', await call('DELETE', '/api/trips/{id}/payments/{paymentId}', { ...tp, paymentId: payment?.id }, a), 204);
expect('delete expense', await call('DELETE', '/api/trips/{id}/expenses/{expenseId}', { ...tp, expenseId: expense?.id }, a), 204);
expect('complete', await call('POST', '/api/trips/{id}/complete', tp, a), 200, (t) => t.status === 'Completed');
const trip2 = (await call('POST', '/api/trips', {}, a, { name: 'Sweep trip to cancel', startDate: '2027-08-01', endDate: '2027-08-01' })).body;
expect('cancel', await call('POST', '/api/trips/{id}/cancel', { id: trip2?.id }, a), 200, (t) => t.status === 'Cancelled');
expect('delete gear lists', await call('DELETE', '/api/trips/gear-lists/{id}', { id: savedList?.id }, a), 204);

// ── Clean up and wrap up groups/routes/trips ─────────────────────────────────────────────────────────────────
console.log('Clean up');
expect('delete trip', await call('DELETE', '/api/trips/{id}', tp, a), 204);
await call('DELETE', '/api/trips/{id}', { id: trip2?.id }, a);
await call('DELETE', '/api/trips/gear-lists/{id}', { id: gearList?.id }, a);
expect('delete route copy', await call('DELETE', '/api/routes/{id}', { id: copy?.id }, b), 204);
expect('delete route', await call('DELETE', '/api/routes/{id}', { id: route?.id }, a), 204);
expectProblem('delete missing route', await call('DELETE', '/api/routes/{id}', { id: missing }, a), 404);
expectProblem('delete group (member, not owner)', await call('DELETE', '/api/groups/{id}', { id: group?.id }, b), 403);
expect('transfer ownership', await call('POST', '/api/groups/{id}/owner', { id: group?.id }, a, { userId: b }), 200);
expect('former owner leaves', await call('POST', '/api/groups/{id}/leave', { id: group?.id }, a), 204);
expect('delete group', await call('DELETE', '/api/groups/{id}', { id: group?.id }, b), 204);
expect('delete hike', await call('DELETE', '/api/activities/{id}', { id: activityId }, a), 204);
await call('PUT', '/api/trails/{id}/moderation', { id: route?.id }, moderator, { hidden: true });
expect('no token → 401', { status: (await fetch(`${base}/api/trips`)).status, text: '' }, 401);

// ── Coverage: every operation in the OpenAPI documents should have been called ──────────────────────────────
const operations = [];
for (const service of ['profile', 'trails', 'activity', 'trips', 'groups', 'routes', 'weather']) {
  const doc = await (await fetch(`${base}/openapi/${service}.json`)).json();
  for (const [path, ops] of Object.entries(doc.paths)) for (const method of Object.keys(ops)) operations.push(`${method.toUpperCase()} ${path}`);
}
const uncalled = operations.filter((o) => !called.has(o));
check(`all ${operations.length} operations called`, uncalled.length === 0, uncalled.join(', '));

console.log(`\n${checks - failed}/${checks} checks passed across ${operations.length - uncalled.length}/${operations.length} operations`);
console.log(`(test trail: ${trail})`);
process.exit(failed ? 1 : 0);
