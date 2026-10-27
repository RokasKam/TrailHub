import { Image } from 'expo-image';
import { useState } from 'react';
import { Alert, Modal, Pressable, ScrollView, StyleSheet, View } from 'react-native';

import { useUploadTrailPhoto } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { Button, Section } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { formatDate } from '@/lib/format';
import { choosePhotoSource } from '@/lib/photos';
import type { TrailDetails } from '@/lib/types';

type Photo = TrailDetails['photos'][number];

/** Horizontal strip of trail photos with an "Add photo" action and a full-screen viewer. */
export function PhotoGallery({ trailId, photos }: { trailId: string; photos: Photo[] }) {
  const upload = useUploadTrailPhoto(trailId);
  const [open, setOpen] = useState<Photo | null>(null);

  const addPhoto = async () => {
    const photo = await choosePhotoSource();
    if (!photo) return;
    upload.mutate(photo, { onError: (e) => Alert.alert('Upload failed', e.message) });
  };

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
            <Pressable key={p.url} onPress={() => setOpen(p)} accessibilityLabel={p.caption ?? 'Trail photo'}>
              <Image source={{ uri: p.url }} style={styles.thumb} contentFit="cover" transition={150} />
            </Pressable>
          ))}
        </ScrollView>
      )}

      <Modal visible={!!open} animationType="fade" onRequestClose={() => setOpen(null)} presentationStyle="fullScreen">
        <Pressable style={styles.viewer} onPress={() => setOpen(null)}>
          {open && <Image source={{ uri: open.url }} style={StyleSheet.absoluteFill} contentFit="contain" />}
          <View style={styles.caption}>
            <ThemedText type="small" style={styles.captionText}>
              {open?.caption ? `${open.caption} · ` : ''}
              {open ? formatDate(open.uploadedAtUtc) : ''} · Tap to close
            </ThemedText>
          </View>
        </Pressable>
      </Modal>
    </Section>
  );
}

const styles = StyleSheet.create({
  strip: { gap: Spacing.two },
  thumb: { width: 200, height: 150, borderRadius: Radius.medium, borderCurve: 'continuous', backgroundColor: '#ddd' },
  viewer: { flex: 1, backgroundColor: '#000' },
  caption: { position: 'absolute', bottom: Spacing.six, left: 0, right: 0, alignItems: 'center' },
  captionText: { color: '#fff' },
});
