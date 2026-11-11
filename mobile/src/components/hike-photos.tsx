import { Image } from 'expo-image';
import { useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, View } from 'react-native';

import { ThemedText } from '@/components/themed-text';
import { Radius, Spacing } from '@/constants/theme';
import { formatDate } from '@/lib/format';
import type { HikePhoto } from '@/lib/types';

/** Horizontal strip of hike photos; tapping one opens it full screen. */
export function HikePhotoStrip({ photos, width = 200, height = 150 }: { photos: HikePhoto[]; width?: number; height?: number }) {
  const [open, setOpen] = useState<HikePhoto | null>(null);
  if (photos.length === 0) return null;
  return (
    <>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.strip}>
        {photos.map((p) => (
          <Pressable key={p.id} onPress={() => setOpen(p)} accessibilityRole="imagebutton" accessibilityLabel={p.caption ?? 'Hike photo'}>
            <Image source={{ uri: p.url }} style={[styles.thumb, { width, height }]} contentFit="cover" transition={150} />
          </Pressable>
        ))}
      </ScrollView>
      <PhotoViewer photo={open} onClose={() => setOpen(null)} />
    </>
  );
}

export function PhotoViewer({ photo, onClose }: { photo: HikePhoto | null; onClose: () => void }) {
  return (
    <Modal visible={!!photo} animationType="fade" onRequestClose={onClose} presentationStyle="fullScreen">
      <Pressable style={styles.viewer} onPress={onClose} accessibilityLabel="Close photo">
        {photo && <Image source={{ uri: photo.url }} style={StyleSheet.absoluteFill} contentFit="contain" />}
        <View style={styles.caption}>
          <ThemedText type="small" style={styles.captionText}>
            {photo?.caption ? `${photo.caption} · ` : ''}
            {photo ? formatDate(photo.addedAtUtc) : ''} · Tap to close
          </ThemedText>
        </View>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  strip: { gap: Spacing.two },
  thumb: { borderRadius: Radius.medium, borderCurve: 'continuous', backgroundColor: '#ddd' },
  viewer: { flex: 1, backgroundColor: '#000' },
  caption: { position: 'absolute', bottom: Spacing.six, left: Spacing.three, right: Spacing.three, alignItems: 'center' },
  captionText: { color: '#fff', textAlign: 'center' },
});
