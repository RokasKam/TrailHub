import { Image } from 'expo-image';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, View } from 'react-native';

import { useDeleteTrailPhoto, useReportTrailPhoto, useUploadTrailPhoto } from '@/api/trails';
import { useSession } from '@/auth/session';
import { HiddenThumbBadge, PhotoViewer } from '@/components/photo-viewer';
import { CONTENT_REPORT_REASONS, ReportSheet } from '@/components/report-sheet';
import { ThemedText } from '@/components/themed-text';
import { Button, Section } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { confirmDelete, type SheetAction, showActions } from '@/lib/action-sheet';
import { describeError } from '@/lib/api';
import { choosePhotoSource } from '@/lib/photos';
import type { TrailPhoto } from '@/lib/types';

/** Horizontal strip of trail photos with an "Add photo" action and a full-screen viewer. Long-press or "…" for Report / Delete. */
export function PhotoGallery({ trailId, photos }: { trailId: string; photos: TrailPhoto[] }) {
  const { user } = useSession();
  const upload = useUploadTrailPhoto(trailId);
  const remove = useDeleteTrailPhoto(trailId);
  const report = useReportTrailPhoto(trailId);
  const [open, setOpen] = useState<TrailPhoto | null>(null);
  const [reporting, setReporting] = useState<TrailPhoto | null>(null);

  const addPhoto = async () => {
    const photo = await choosePhotoSource();
    if (!photo) return;
    upload.mutate(photo, { onError: (e) => Alert.alert('Upload failed', describeError(e)) });
  };

  const actionsFor = (p: TrailPhoto): SheetAction[] => [
    ...(p.uploadedBy !== user?.id ? [{ label: 'Report photo', onPress: () => setReporting(p) }] : []),
    ...(p.canDelete
      ? [
          {
            label: 'Delete photo',
            destructive: true,
            onPress: () =>
              confirmDelete('Delete photo?', 'It’s removed from the trail for everyone. This can’t be undone.', () =>
                remove.mutate(p.id, { onSuccess: () => setOpen((o) => (o?.id === p.id ? null : o)), onError: (e) => Alert.alert('Couldn’t delete photo', describeError(e)) }),
              ),
          },
        ]
      : []),
  ];
  const showPhotoActions = (p: TrailPhoto) => showActions(p.caption ?? undefined, actionsFor(p));

  // Read the id during render, not inside the callback: the React Compiler reads a callback's dependencies (here
  // `reporting.id`) eagerly when memoizing it, which crashes while nothing is being reported.
  const reportingId = reporting?.id;
  const sheet = (
    <ReportSheet
      visible={!!reporting}
      onClose={() => setReporting(null)}
      title="Report photo"
      reasons={CONTENT_REPORT_REASONS}
      onSubmit={(r) => report.mutateAsync({ photoId: reportingId!, ...r })}
    />
  );
  const openActions = open ? actionsFor(open) : [];

  return (
    <Section title="Photos" action={<Button size="small" variant="tinted" icon="camera.fill" label={upload.isPending ? 'Uploading…' : 'Add'} onPress={addPhoto} loading={upload.isPending} />}>
      {photos.length === 0 && (
        <ThemedText type="small" themeColor="textSecondary">
          No photos yet. Be the first to share the view.
        </ThemedText>
      )}
      {photos.length > 0 && (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.strip}>
          {[...photos].reverse().map((p) => (
            <Pressable
              key={p.id}
              onPress={() => setOpen(p)}
              onLongPress={() => showPhotoActions(p)}
              accessibilityRole="imagebutton"
              accessibilityLabel={p.caption ?? 'Trail photo'}
              accessibilityHint="Long-press for more actions">
              <View>
                <Image source={{ uri: p.url }} style={[styles.thumb, p.hidden && styles.dimmed]} contentFit="cover" transition={150} />
                {p.hidden && <HiddenThumbBadge />}
                {remove.isPending && remove.variables === p.id && (
                  <View style={styles.busy}>
                    <ActivityIndicator color="#fff" />
                  </View>
                )}
              </View>
            </Pressable>
          ))}
        </ScrollView>
      )}

      <PhotoViewer
        photo={open && { url: open.url, caption: open.caption, dateUtc: open.uploadedAtUtc, hidden: open.hidden }}
        onClose={() => setOpen(null)}
        onMore={openActions.length ? () => showPhotoActions(open!) : undefined}
        busy={remove.isPending}>
        {/* iOS can only present the sheet from the topmost modal, so it lives inside the viewer while that's open. */}
        {open && sheet}
      </PhotoViewer>
      {!open && sheet}
    </Section>
  );
}

const styles = StyleSheet.create({
  strip: { gap: Spacing.two },
  thumb: { width: 200, height: 150, borderRadius: Radius.medium, borderCurve: 'continuous', backgroundColor: '#ddd' },
  dimmed: { opacity: 0.45 },
  busy: { ...StyleSheet.absoluteFill, borderRadius: Radius.medium, backgroundColor: 'rgba(0,0,0,0.45)', alignItems: 'center', justifyContent: 'center' },
});
