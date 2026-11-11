import { Image } from 'expo-image';
import type { PropsWithChildren } from 'react';
import { ActivityIndicator, Modal, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ThemedText } from '@/components/themed-text';
import { Icon } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { formatDate } from '@/lib/format';
import type { AppIcon } from '@/lib/icons';

export type ViewerPhoto = { url: string; caption: string | null; dateUtc: string; hidden?: boolean };

export const HIDDEN_AFTER_REPORTS = 'Hidden after reports';

/**
 * Full-screen photo. Tap the photo or × to close; "…" (when `onMore` is given) opens the photo's actions.
 * `children` render inside the modal, e.g. a report sheet opened from the actions.
 */
export function PhotoViewer({ photo, onClose, onMore, busy, children }: PropsWithChildren<{ photo: ViewerPhoto | null; onClose: () => void; onMore?: () => void; busy?: boolean }>) {
  const insets = useSafeAreaInsets();
  return (
    <Modal visible={!!photo} animationType="fade" onRequestClose={onClose} presentationStyle="fullScreen">
      <View style={styles.viewer}>
        <Pressable style={StyleSheet.absoluteFill} onPress={onClose} accessibilityLabel="Close photo">
          {photo && <Image source={{ uri: photo.url }} style={[StyleSheet.absoluteFill, photo.hidden && { opacity: 0.6 }]} contentFit="contain" />}
        </Pressable>

        <View style={[styles.topBar, { top: insets.top + Spacing.two }]}>
          <RoundButton icon="xmark" label="Close photo" onPress={onClose} />
          {onMore && (busy ? <ActivityIndicator color="#fff" style={styles.round} /> : <RoundButton icon="ellipsis" label="Photo actions" onPress={onMore} />)}
        </View>

        <View style={[styles.bottom, { bottom: insets.bottom + Spacing.four, pointerEvents: 'none' }]}>
          {photo?.hidden && (
            <View style={styles.hiddenNote}>
              <Icon name="eye.slash.fill" size={12} color="#FCD34D" />
              <ThemedText type="caption" style={{ color: '#FCD34D', fontWeight: 700 }}>
                {HIDDEN_AFTER_REPORTS} · only you and moderators see it
              </ThemedText>
            </View>
          )}
          <ThemedText type="small" style={styles.captionText}>
            {photo?.caption ? `${photo.caption} · ` : ''}
            {photo ? formatDate(photo.dateUtc) : ''}
          </ThemedText>
        </View>
        {children}
      </View>
    </Modal>
  );
}

function RoundButton({ icon, label, onPress }: { icon: AppIcon; label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} hitSlop={8} onPress={onPress} style={({ pressed }) => [styles.round, { opacity: pressed ? 0.6 : 1 }]}>
      <Icon name={icon} size={17} color="#fff" weight="bold" />
    </Pressable>
  );
}

/** Small badge on a thumbnail the viewer can still see although it's hidden; `compact` for small tiles. */
export function HiddenThumbBadge({ compact }: { compact?: boolean }) {
  return (
    <View style={[styles.thumbBadge, { pointerEvents: 'none' }]} accessibilityLabel={HIDDEN_AFTER_REPORTS}>
      <Icon name="eye.slash.fill" size={11} color="#fff" />
      <ThemedText type="caption" style={{ color: '#fff', fontWeight: 700 }}>
        {compact ? 'Hidden' : HIDDEN_AFTER_REPORTS}
      </ThemedText>
    </View>
  );
}

const styles = StyleSheet.create({
  viewer: { flex: 1, backgroundColor: '#000' },
  topBar: { position: 'absolute', left: Spacing.three, right: Spacing.three, flexDirection: 'row', justifyContent: 'space-between' },
  round: { width: 38, height: 38, borderRadius: 19, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(255,255,255,0.18)' },
  bottom: { position: 'absolute', left: Spacing.three, right: Spacing.three, alignItems: 'center', gap: Spacing.two },
  hiddenNote: { flexDirection: 'row', alignItems: 'center', gap: 5, paddingHorizontal: 10, paddingVertical: 4, borderRadius: Radius.pill, backgroundColor: 'rgba(120,53,15,0.75)' },
  captionText: { color: '#fff', textAlign: 'center' },
  thumbBadge: {
    position: 'absolute',
    left: 6,
    bottom: 6,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: Radius.pill,
    backgroundColor: 'rgba(120,53,15,0.85)',
  },
});
