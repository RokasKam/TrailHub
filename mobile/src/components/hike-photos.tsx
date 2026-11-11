import { Image } from 'expo-image';
import { type PropsWithChildren, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, View } from 'react-native';

import { useDeleteHikePhoto } from '@/api/activities';
import { useModerateContent, useReportHikePhoto } from '@/api/feed';
import { HiddenThumbBadge, PhotoViewer } from '@/components/photo-viewer';
import { CONTENT_REPORT_REASONS, ReportSheet } from '@/components/report-sheet';
import { Radius, Spacing } from '@/constants/theme';
import { confirmDelete, type SheetAction, showActions } from '@/lib/action-sheet';
import { describeError } from '@/lib/api';
import type { HikePhoto } from '@/lib/types';

/**
 * Horizontal strip of hike photos; tapping one opens it full screen. With `post` (a feed post), photos get
 * Report / Delete as the API allows (long-press, or "…" in the viewer), and hidden ones show dimmed.
 */
export function HikePhotoStrip({ photos, post, width = 200, height = 150 }: { photos: HikePhoto[]; post?: { activityId: string; isMine: boolean }; width?: number; height?: number }) {
  const [open, setOpen] = useState<HikePhoto | null>(null);
  const [reporting, setReporting] = useState<HikePhoto | null>(null);
  // Mutations only run when invoked, so these are harmless without a post.
  const postId = post?.activityId ?? '';
  const report = useReportHikePhoto(postId);
  const removeOwn = useDeleteHikePhoto(postId);
  const moderate = useModerateContent();
  if (photos.length === 0) return null;

  const deleting = removeOwn.isPending ? removeOwn.variables : moderate.isPending && moderate.variables?.action === 'Delete' ? moderate.variables.contentId : undefined;

  // The author deletes through their hike; anyone else allowed to (a moderator) through moderation.
  const remove = (p: HikePhoto) => {
    const done = { onSuccess: () => setOpen((o) => (o?.id === p.id ? null : o)), onError: (e: Error) => Alert.alert('Couldn’t delete photo', describeError(e)) };
    if (post?.isMine) removeOwn.mutate(p.id, done);
    else moderate.mutate({ kind: 'photos', contentId: p.id, action: 'Delete' }, done);
  };
  const actionsFor = (p: HikePhoto): SheetAction[] =>
    post
      ? [
          ...(p.canReport ? [{ label: 'Report photo', onPress: () => setReporting(p) }] : []),
          ...(p.canDelete
            ? [{ label: 'Delete photo', destructive: true, onPress: () => confirmDelete('Delete photo?', 'It’s removed from the hike for everyone. This can’t be undone.', () => remove(p)) }]
            : []),
        ]
      : [];
  const show = (p: HikePhoto) => showActions(p.caption ?? undefined, actionsFor(p));

  // Read the id during render, not inside the callback: the React Compiler reads a callback's dependencies (here
  // `reporting.id`) eagerly when memoizing it, which crashes while nothing is being reported.
  const reportingId = reporting?.id;
  const sheet = post && (
    <ReportSheet
      visible={!!reporting}
      onClose={() => setReporting(null)}
      title="Report photo"
      reasons={CONTENT_REPORT_REASONS}
      onSubmit={(r) => report.mutateAsync({ photoId: reportingId!, ...r })}
    />
  );

  return (
    <>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.strip}>
        {photos.map((p) => (
          <Pressable
            key={p.id}
            onPress={() => setOpen(p)}
            onLongPress={post ? () => show(p) : undefined}
            accessibilityRole="imagebutton"
            accessibilityLabel={p.caption ?? 'Hike photo'}>
            <View>
              <Image source={{ uri: p.url }} style={[styles.thumb, { width, height }, p.hidden && styles.dimmed]} contentFit="cover" transition={150} />
              {p.hidden && <HiddenThumbBadge />}
              {deleting === p.id && (
                <View style={styles.busy}>
                  <ActivityIndicator color="#fff" />
                </View>
              )}
            </View>
          </Pressable>
        ))}
      </ScrollView>
      <HikePhotoViewer photo={open} onClose={() => setOpen(null)} onMore={open && actionsFor(open).length ? () => show(open) : undefined} busy={!!open && deleting === open.id}>
        {/* iOS can only present the sheet from the topmost modal, so it lives inside the viewer while that's open. */}
        {open && sheet}
      </HikePhotoViewer>
      {!open && sheet}
    </>
  );
}

export function HikePhotoViewer({ photo, onClose, onMore, busy, children }: PropsWithChildren<{ photo: HikePhoto | null; onClose: () => void; onMore?: () => void; busy?: boolean }>) {
  return (
    <PhotoViewer photo={photo && { url: photo.url, caption: photo.caption, dateUtc: photo.addedAtUtc, hidden: photo.hidden }} onClose={onClose} onMore={onMore} busy={busy}>
      {children}
    </PhotoViewer>
  );
}

const styles = StyleSheet.create({
  strip: { gap: Spacing.two },
  thumb: { borderRadius: Radius.medium, borderCurve: 'continuous', backgroundColor: '#ddd' },
  dimmed: { opacity: 0.45 },
  busy: { ...StyleSheet.absoluteFill, borderRadius: Radius.medium, backgroundColor: 'rgba(0,0,0,0.45)', alignItems: 'center', justifyContent: 'center' },
});
