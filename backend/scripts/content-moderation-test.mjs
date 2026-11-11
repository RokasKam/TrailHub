// Reporting and moderating content: trail photos (Trails) and hike comments and photos (Activity). Reports, the
// own-content rule, auto-hide after three reports, who still sees hidden items, the moderators' queues, Restore / Hide /
// Delete, and who may delete without moderation. Needs Trails, Activity and Groups in Auth:Mode=Development with
// Moderation:ModeratorUserIds__0=<MODERATOR> on Trails and Activity, and the photo store running:
//   MODERATOR=moderation-mod node scripts/content-moderation-test.mjs
// BASE defaults to the gateway.
import { readFile } from 'node:fs/promises';

const base = process.env.BASE ?? 'http://localhost:8080';
const moderator = process.env.MODERATOR ?? 'moderation-mod';
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

const png = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='), (c) => c.charCodeAt(0));
const photoForm = (caption) => {
  const f = new FormData();
  f.append('file', new Blob([png], { type: 'image/png' }), 'dot.png');
  if (caption) f.append('caption', caption);
  return f;
};
const fileStatus = (url) => fetch(url).then((r) => r.status).catch(() => 0);

const stamp = Date.now();
const missing = '00000000-0000-0000-0000-000000000001';

// ── Trail photos ───────────────────────────────────────────────────────────────────────────────────────────
console.log('Trail photos');
const author = `cm-author-${stamp}`, uploader = `cm-uploader-${stamp}`, someone = `cm-someone-${stamp}`;
const reporters = [0, 1, 2].map((i) => `cm-reporter-${i}-${stamp}`);

const gpxForm = new FormData();
gpxForm.append('file', new Blob([await readFile(new URL('../samples/demo-loop.gpx', import.meta.url))]), 'demo.gpx');
gpxForm.append('name', `Content moderation ${stamp}`);
gpxForm.append('region', 'Test region');
gpxForm.append('difficulty', 'Moderate');
const imported = await call('POST', '/api/trails/import', author, null, gpxForm);
const trailId = imported.body?.id;
check('the author imports a trail', imported.status === 201 && !!trailId);
const trailPhotos = (user) => call('GET', `/api/trails/${trailId}`, user).then((r) => r.body?.photos ?? []);

const uploaded = await call('POST', `/api/trails/${trailId}/photos/upload`, uploader, null, photoForm('Summit'));
const photo = uploaded.body;
check('an upload returns the photo with its id', uploaded.status === 201 && !!photo?.id && photo.uploadedBy === uploader && photo.hidden === false && photo.canDelete === true && photo.caption === 'Summit');
const second = (await call('POST', `/api/trails/${trailId}/photos/upload`, uploader, null, photoForm('Second'))).body;
const third = (await call('POST', `/api/trails/${trailId}/photos/upload`, uploader, null, photoForm('Third'))).body;
const asSomeone = (await trailPhotos(someone)).find((p) => p.id === photo?.id);
check('trail details list photos with id, uploader and flags', asSomeone?.uploadedBy === uploader && asSomeone.hidden === false && asSomeone.canDelete === false && !!asSomeone.url);
check("the trail's author can delete any photo", (await trailPhotos(author)).every((p) => p.canDelete));

const report = (user, photoId, body = { reason: 'Inappropriate' }) => call('POST', `/api/trails/${trailId}/photos/${photoId}/report`, user, body);
check("uploaders can't report their own photo", problem(await report(uploader, photo?.id), 400, 'Trail.CantReportOwnContent'));
check('report details are limited', problem(await report(someone, photo?.id, { reason: 'Spam', details: 'x'.repeat(1001) }), 400, 'Trail.InvalidReport'));
check('reporting a missing photo is 404', problem(await report(someone, missing), 404, 'Trail.PhotoNotFound'));
check('reporting on a missing trail is 404', problem(await call('POST', `/api/trails/${missing}/photos/${photo?.id}/report`, someone, { reason: 'Spam' }), 404, 'Trail.NotFound'));
check('a report is filed', (await report(reporters[0], photo?.id, { reason: 'Harassment', details: 'Mean caption' })).status === 204);
await report(reporters[1], photo?.id, { reason: 'Spam' });
await report(reporters[0], photo?.id, { reason: 'Harassment' });
check('two people reporting (one twice) keep it visible', (await trailPhotos(someone)).some((p) => p.id === photo?.id));
await report(reporters[2], photo?.id, { reason: 'Other' });
check('the third report hides it from others', !(await trailPhotos(someone)).some((p) => p.id === photo?.id));
const hiddenForUploader = (await trailPhotos(uploader)).find((p) => p.id === photo?.id);
check('the uploader still sees it, marked hidden', hiddenForUploader?.hidden === true);
check('moderators still see it', (await trailPhotos(moderator)).find((p) => p.id === photo?.id)?.hidden === true);
check("others can't report a hidden photo (404)", problem(await report(someone, photo?.id), 404, 'Trail.PhotoNotFound'));

check('non-moderators get 403 for the photo queue', problem(await call('GET', '/api/trails/moderation/photos', someone), 403, 'Trail.NotModerator'));
const photoQueue = await call('GET', '/api/trails/moderation/photos', moderator);
const queued = photoQueue.body?.find?.((x) => x.photoId === photo?.id);
check(
  'moderators see it with its 3 reports',
  photoQueue.status === 200 && queued?.trailId === trailId && queued.trailName === `Content moderation ${stamp}` && queued.openReports === 3 && queued.hiddenBy === 'Reports'
    && queued.uploadedBy === uploader && queued.caption === 'Summit' && !!queued.url && queued.reports.some((r) => r.userId === reporters[0] && r.reason === 'Harassment' && !!r.createdAtUtc),
);
const moderate = (user, photoId, action) => call('PUT', `/api/trails/${trailId}/photos/${photoId}/moderation`, user, { action });
check("non-moderators can't moderate (403)", problem(await moderate(uploader, photo?.id, 'Restore'), 403, 'Trail.NotModerator'));
check('a moderator restores it', (await moderate(moderator, photo?.id, 'Restore')).status === 204);
check('restored photos are back for everyone', (await trailPhotos(someone)).find((p) => p.id === photo?.id)?.hidden === false);
check('restoring resolves its reports', !(await call('GET', '/api/trails/moderation/photos', moderator)).body?.some((x) => x.photoId === photo?.id));
check('a moderator hides it', (await moderate(moderator, photo?.id, 'Hide')).status === 204);
check('moderator-hidden photos stay in the queue', (await call('GET', '/api/trails/moderation/photos', moderator)).body?.some((x) => x.photoId === photo?.id && x.hiddenBy === 'Moderator' && x.openReports === 0));
check('and are hidden from others', !(await trailPhotos(someone)).some((p) => p.id === photo?.id));
check('moderating a missing photo is 404', problem(await moderate(moderator, missing, 'Hide'), 404, 'Trail.PhotoNotFound'));

check("others can't delete a photo (403)", problem(await call('DELETE', `/api/trails/${trailId}/photos/${second?.id}`, someone), 403, 'Trail.NotPhotoOwner'));
check("the trail's author deletes a photo", (await call('DELETE', `/api/trails/${trailId}/photos/${second?.id}`, author)).status === 204);
check('the uploader deletes their own photo', (await call('DELETE', `/api/trails/${trailId}/photos/${third?.id}`, uploader)).status === 204);
check('deleting it again is 404', problem(await call('DELETE', `/api/trails/${trailId}/photos/${third?.id}`, uploader), 404, 'Trail.PhotoNotFound'));
check('a moderator deletes the reported photo', (await moderate(moderator, photo?.id, 'Delete')).status === 204);
check('deleted photos are gone', (await trailPhotos(author)).length === 0);
check('their stored files are deleted', (await fileStatus(photo?.url)) !== 200 && (await fileStatus(second?.url)) !== 200);
check('deleted photos leave the queue', !(await call('GET', '/api/trails/moderation/photos', moderator)).body?.some((x) => x.trailId === trailId));
check('the trail queue is unchanged', (await call('GET', '/api/trails/moderation', moderator)).status === 200);

// ── Hike comments and photos ───────────────────────────────────────────────────────────────────────────────
console.log('Hike comments and photos');
const anna = `cm-a-${stamp}`, ben = `cm-b-${stamp}`, cleo = `cm-c-${stamp}`, dan = `cm-d-${stamp}`, xena = `cm-x-${stamp}`;
const group = (await call('POST', '/api/groups', anna, { name: 'Moderation hikers', displayName: 'Anna' })).body;
for (const [user, name] of [[ben, 'Ben'], [cleo, 'Cleo'], [dan, 'Dan']]) await call('POST', '/api/groups/join', user, { inviteCode: group?.inviteCode, displayName: name });

const id = crypto.randomUUID();
const t0 = Date.now() - 3 * 3600_000;
const points = Array.from({ length: 10 }, (_, i) => ({ latitude: 46 + i * 0.001, longitude: 7.5, elevation: 1000 + i * 5, timestampUtc: new Date(t0 + i * 60_000).toISOString() }));
await call('PUT', `/api/activities/${id}`, anna, { name: 'Moderation hike', segments: [points] });
await eventually(() => call('GET', '/api/activities/share-targets', anna), (r) => r.body?.some?.((g) => g.groupId === group?.id && g.memberCount === 4));
check('a hike is shared with the group', (await call('PUT', `/api/activities/${id}/sharing`, anna, { groupIds: [group?.id], privacy: 'Full' })).status === 200);
const post = (user) => call('GET', `/api/activities/feed/${id}`, user);
await eventually(() => post(dan), (r) => r.status === 200);

const comment = (await call('POST', `/api/activities/feed/${id}/comments`, ben, { text: 'Rude remark' })).body;
const asCleo = (await post(cleo)).body?.comments?.find((c) => c.id === comment?.id);
check('comments say whether I can report them', asCleo?.canReport === true && asCleo.hidden === false && comment?.canReport === false);

const reportComment = (user, commentId, body = { reason: 'Harassment' }, postId = id) => call('POST', `/api/activities/feed/${postId}/comments/${commentId}/report`, user, body);
check("writers can't report their own comment", problem(await reportComment(ben, comment?.id), 400, 'Activity.CantReportOwnContent'));
check('report details are limited', problem(await reportComment(cleo, comment?.id, { reason: 'Spam', details: 'x'.repeat(1001) }), 400, 'Activity.InvalidReport'));
check('reporting a missing comment is 404', problem(await reportComment(cleo, missing), 404, 'Activity.CommentNotFound'));
check("people who can't see the post get 404", problem(await reportComment(xena, comment?.id), 404, 'Activity.NotFound'));
check('a comment is reported', (await reportComment(cleo, comment?.id, { reason: 'Harassment', details: 'Not nice' })).status === 204);
await reportComment(dan, comment?.id);
await reportComment(cleo, comment?.id);
check('two reporters keep it visible', (await post(cleo)).body?.comments?.some((c) => c.id === comment?.id));
await reportComment(anna, comment?.id, { reason: 'Inappropriate' });
const cleoAfter = (await post(cleo)).body;
check('the third report hides it from others', !cleoAfter?.comments?.some((c) => c.id === comment?.id) && cleoAfter?.commentCount === 0);
check('hidden comments leave the feed count', (await call('GET', '/api/activities/feed', cleo)).body?.items?.find((p) => p.activityId === id)?.commentCount === 0);
const benView = (await post(ben)).body;
check('the writer still sees it, marked hidden', benView?.comments?.find((c) => c.id === comment?.id)?.hidden === true && benView.commentCount === 1);
check("others can't report a hidden comment (404)", problem(await reportComment(dan, comment?.id), 404, 'Activity.CommentNotFound'));

const hikePhoto = (await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm('Ridge'))).body;
check("the author's photo response has the new flags", hikePhoto?.hidden === false && hikePhoto.canReport === false && hikePhoto.canDelete === true);
const benPhoto = (await post(ben)).body?.photos?.find((p) => p.id === hikePhoto?.id);
check('members can report but not delete it', benPhoto?.canReport === true && benPhoto.canDelete === false && benPhoto.hidden === false);
const reportPhoto = (user, photoId, body = { reason: 'Inappropriate' }) => call('POST', `/api/activities/feed/${id}/photos/${photoId}/report`, user, body);
check("the hike's author can't report its photos", problem(await reportPhoto(anna, hikePhoto?.id), 400, 'Activity.CantReportOwnContent'));
check('reporting a missing photo is 404', problem(await reportPhoto(ben, missing), 404, 'Activity.PhotoNotFound'));
for (const user of [ben, cleo]) await reportPhoto(user, hikePhoto?.id);
check('two reports keep the photo visible', (await post(dan)).body?.photos?.some((p) => p.id === hikePhoto?.id));
check('a photo is reported', (await reportPhoto(dan, hikePhoto?.id, { reason: 'Spam' })).status === 204);
check('the third report hides it from others', !(await post(ben)).body?.photos?.some((p) => p.id === hikePhoto?.id));
check('the author still sees it, marked hidden', (await post(anna)).body?.photos?.find((p) => p.id === hikePhoto?.id)?.hidden === true);
check("the author's hike page marks it hidden", (await call('GET', `/api/activities/${id}`, anna)).body?.photos?.find((p) => p.id === hikePhoto?.id)?.hidden === true);
check("others can't report a hidden photo (404)", problem(await reportPhoto(ben, hikePhoto?.id), 404, 'Activity.PhotoNotFound'));

check('non-moderators get 403 for the queue', problem(await call('GET', '/api/activities/moderation', anna), 403, 'Activity.NotModerator'));
const queue = (await call('GET', '/api/activities/moderation', moderator)).body;
const queuedComment = queue?.find?.((x) => x.contentId === comment?.id);
const queuedPhoto = queue?.find?.((x) => x.contentId === hikePhoto?.id);
check(
  'moderators see the comment with its reports',
  queuedComment?.kind === 'Comment' && queuedComment.activityId === id && queuedComment.text === 'Rude remark' && queuedComment.url === null
    && queuedComment.authorId === ben && queuedComment.authorName === 'Ben' && queuedComment.hiddenBy === 'Reports' && queuedComment.openReports === 3
    // Cleo reported twice; the second report (no details) replaced the first, so she has exactly one.
    && queuedComment.reports.filter((r) => r.userId === cleo).length === 1 && queuedComment.reports.some((r) => r.userId === cleo && r.reason === 'Harassment'),
);
check(
  'and the photo',
  queuedPhoto?.kind === 'HikePhoto' && queuedPhoto.text === null && queuedPhoto.url?.includes('hikes/') && queuedPhoto.authorId === anna && queuedPhoto.authorName === 'Anna'
    && queuedPhoto.hiddenBy === 'Reports' && queuedPhoto.openReports === 3,
);

const moderateContent = (user, kind, contentId, action) => call('PUT', `/api/activities/moderation/${kind}/${contentId}`, user, { action });
check("non-moderators can't moderate (403)", problem(await moderateContent(ben, 'comments', comment?.id, 'Restore'), 403, 'Activity.NotModerator'));
check('a moderator restores the comment', (await moderateContent(moderator, 'comments', comment?.id, 'Restore')).status === 204);
check('restored comments are back for everyone', (await post(cleo)).body?.comments?.find((c) => c.id === comment?.id)?.hidden === false);
check('restoring resolves its reports', !(await call('GET', '/api/activities/moderation', moderator)).body?.some((x) => x.contentId === comment?.id));
check('a moderator hides the comment', (await moderateContent(moderator, 'comments', comment?.id, 'Hide')).status === 204);
check('moderator-hidden comments stay in the queue', (await call('GET', '/api/activities/moderation', moderator)).body?.some((x) => x.contentId === comment?.id && x.hiddenBy === 'Moderator' && x.openReports === 0));
check('a moderator deletes the comment', (await moderateContent(moderator, 'comments', comment?.id, 'Delete')).status === 204);
check('deleted comments are gone, even for their writer', !(await post(ben)).body?.comments?.some((c) => c.id === comment?.id));
check('moderating a deleted comment is 404', problem(await moderateContent(moderator, 'comments', comment?.id, 'Restore'), 404, 'Activity.CommentNotFound'));

check('a moderator restores the photo', (await moderateContent(moderator, 'photos', hikePhoto?.id, 'Restore')).status === 204);
check('restored photos are back for members', (await post(ben)).body?.photos?.find((p) => p.id === hikePhoto?.id)?.hidden === false);
check('a moderator deletes the photo', (await moderateContent(moderator, 'photos', hikePhoto?.id, 'Delete')).status === 204);
check('deleted photos are gone from the hike', !(await call('GET', `/api/activities/${id}`, anna)).body?.photos?.some((p) => p.id === hikePhoto?.id));
check('and their stored file is deleted', (await fileStatus(hikePhoto?.url)) !== 200);
check('moderating a missing photo is 404', problem(await moderateContent(moderator, 'photos', hikePhoto?.id, 'Hide'), 404, 'Activity.PhotoNotFound'));
check('the queue is clear of them', !(await call('GET', '/api/activities/moderation', moderator)).body?.some((x) => x.activityId === id));
check('unknown content kinds are 404', (await moderateContent(moderator, 'likes', missing, 'Hide')).status === 404);

// Moderators can delete directly too, without reports.
const another = (await call('POST', `/api/activities/feed/${id}/comments`, cleo, { text: 'Delete me' })).body;
check("members still can't delete others' comments", problem(await call('DELETE', `/api/activities/feed/${id}/comments/${another?.id}`, dan), 403, 'Activity.NotCommentAuthor'));
check('moderators can delete any comment', (await call('DELETE', `/api/activities/feed/${id}/comments/${another?.id}`, moderator)).status === 204);
const anotherPhoto = (await call('POST', `/api/activities/${id}/photos`, anna, null, photoForm())).body;
check("members can't delete the hike's photos", (await call('DELETE', `/api/activities/${id}/photos/${anotherPhoto?.id}`, ben)).status === 404);
check("moderators can delete a hike's photos", (await call('DELETE', `/api/activities/${id}/photos/${anotherPhoto?.id}`, moderator)).status === 204);

// Clean up.
await call('DELETE', `/api/activities/${id}`, anna);
await call('DELETE', `/api/groups/${group?.id}`, anna);
await call('PUT', `/api/trails/${trailId}/moderation`, moderator, { hidden: true });
console.log(`\n(test trail: ${trailId}, hike: ${id})`);
console.log(failed ? `${failed} check(s) failed` : 'All content moderation checks passed');
process.exit(failed ? 1 : 0);
