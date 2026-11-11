import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, View } from 'react-native';

import { MAX_HIKE_PHOTOS, useActivity, useDeleteHikePhoto, useSaveSharing, useShareTargets, useStopSharing, useUploadHikePhoto } from '@/api/activities';
import { PRIVACY_LABELS } from '@/components/feed-post-card';
import { PhotoViewer } from '@/components/hike-photos';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Chip, EmptyState, Icon, Input, QueryState, Section, Segmented } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';
import { choosePhotoSource } from '@/lib/photos';
import type { ActivityDetails, HikePhoto, ShareTarget, TrackPrivacy } from '@/lib/types';

const MAX_GROUPS = 10;
const MAX_CAPTION = 500;

const PRIVACY_HINTS: Record<TrackPrivacy, string> = {
  Full: 'Group members see the whole track.',
  HideEnds: 'The first and last 200 m are hidden, so your start (often home) stays private.',
  StatsOnly: 'No map. Members see distance, climb and time only.',
};

/**
 * Share a hike with some of your groups: pick groups, write a caption, choose how much of the track
 * members see, and add photos. Also used to update or stop sharing an already shared hike.
 */
export function ShareHike({ activityId }: { activityId: string }) {
  const activity = useActivity(activityId);
  const targets = useShareTargets();

  if (!activity.data || !targets.data) {
    const error = activity.error ?? targets.error;
    return (
      <QueryState
        isLoading={activity.isLoading || targets.isLoading}
        error={error}
        onRetry={() => {
          void activity.refetch();
          void targets.refetch();
        }}
      />
    );
  }
  // Mounted once the data is in, so the form starts from the hike's current sharing.
  return <ShareForm activity={activity.data} targets={targets.data} />;
}

function ShareForm({ activity, targets }: { activity: ActivityDetails; targets: ShareTarget[] }) {
  const sharing = activity.sharing;
  const [groupIds, setGroupIds] = useState<string[]>(() => sharing?.groupIds ?? (targets.length === 1 ? [targets[0].groupId] : []));
  const [caption, setCaption] = useState(sharing?.caption ?? '');
  const [privacy, setPrivacy] = useState<TrackPrivacy>(sharing?.privacy ?? 'HideEnds');
  const save = useSaveSharing(activity.id);
  const stop = useStopSharing(activity.id);

  const toggleGroup = (id: string) =>
    setGroupIds((ids) => (ids.includes(id) ? ids.filter((g) => g !== id) : ids.length >= MAX_GROUPS ? ids : [...ids, id]));

  const confirmStop = () =>
    Alert.alert('Stop sharing?', 'Your groups won’t see this hike anymore. Likes and comments are kept if you share it again.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Stop sharing',
        style: 'destructive',
        onPress: () => stop.mutate(undefined, { onSuccess: () => router.back(), onError: (e) => Alert.alert('Couldn’t stop sharing', describeError(e)) }),
      },
    ]);

  // Groups the hike was shared with that you've since left can't be shared with again; drop them silently.
  const knownIds = new Set(targets.map((t) => t.groupId));
  const selected = groupIds.filter((id) => knownIds.has(id));

  const submit = () =>
    save.mutate(
      { groupIds: selected, caption: caption.trim() || null, privacy },
      { onSuccess: () => router.back(), onError: (e) => Alert.alert(sharing ? 'Couldn’t update sharing' : 'Couldn’t share hike', describeError(e)) },
    );

  return (
    <>
      <View style={{ gap: 4 }}>
        <ThemedText type="subtitle" numberOfLines={2}>
          {activity.name}
        </ThemedText>
        <ThemedText type="small" themeColor="textSecondary">
          {sharing ? `Shared with ${sharing.groupIds.length} group${sharing.groupIds.length === 1 ? '' : 's'}` : 'Only you can see this hike until you share it.'}
        </ThemedText>
      </View>

      <Section title="Groups">
        {targets.length === 0 ? (
          <Card style={{ alignItems: 'center' }}>
            <EmptyState icon="person.3" title="You’re not in any groups yet" message="Hikes are shared with your groups. Create one or join with an invite code." />
            <Button label="Go to Groups" variant="tinted" icon="person.3" size="small" onPress={() => router.push('/groups')} />
          </Card>
        ) : (
          <View style={styles.chips}>
            {targets.map((t) => (
              <Chip
                key={t.groupId}
                label={`${t.name} · ${t.memberCount}`}
                icon={selected.includes(t.groupId) ? 'checkmark' : 'person.2'}
                selected={selected.includes(t.groupId)}
                onPress={() => toggleGroup(t.groupId)}
              />
            ))}
          </View>
        )}
      </Section>

      <Section title="Caption">
        <Input
          icon="pencil"
          placeholder="How was it? (optional)"
          value={caption}
          onChangeText={setCaption}
          multiline
          maxLength={MAX_CAPTION}
          style={styles.caption}
        />
        {caption.length > MAX_CAPTION - 100 && (
          <ThemedText type="caption" themeColor="textSecondary" style={{ textAlign: 'right' }}>
            {caption.length}/{MAX_CAPTION}
          </ThemedText>
        )}
      </Section>

      <Section title="Map">
        <Segmented<TrackPrivacy>
          options={[
            { value: 'Full', label: 'Full map', icon: 'map' },
            { value: 'HideEnds', label: 'Hide ends', icon: 'eye.slash' },
            { value: 'StatsOnly', label: 'Stats only', icon: 'lock' },
          ]}
          value={privacy}
          onChange={setPrivacy}
        />
        <ThemedText type="small" themeColor="textSecondary">
          {PRIVACY_LABELS[privacy]}: {PRIVACY_HINTS[privacy]} You always see your full track.
        </ThemedText>
      </Section>

      <HikePhotoEditor activityId={activity.id} photos={activity.photos} />

      <View style={styles.buttons}>
        <Button
          label={sharing ? 'Update sharing' : 'Share'}
          icon={sharing ? 'checkmark' : 'square.and.arrow.up'}
          size="large"
          disabled={selected.length === 0 || stop.isPending}
          loading={save.isPending}
          onPress={submit}
        />
        {selected.length === 0 && targets.length > 0 && (
          <ThemedText type="caption" themeColor="textSecondary" style={{ textAlign: 'center' }}>
            Pick at least one group.
          </ThemedText>
        )}
        {sharing && <Button label="Stop sharing" variant="danger" disabled={save.isPending} loading={stop.isPending} onPress={confirmStop} />}
      </View>
    </>
  );
}

type Upload = { key: string; uri: string };

/** Photos on the hike: each one uploads right away (with a spinner on its tile) and can be removed. */
export function HikePhotoEditor({ activityId, photos }: { activityId: string; photos: HikePhoto[] }) {
  const theme = useTheme();
  const upload = useUploadHikePhoto(activityId);
  const remove = useDeleteHikePhoto(activityId);
  const [uploads, setUploads] = useState<Upload[]>([]);
  const [removing, setRemoving] = useState<string | null>(null);
  const [open, setOpen] = useState<HikePhoto | null>(null);
  const count = photos.length + uploads.length;

  const add = async () => {
    const photo = await choosePhotoSource();
    if (!photo) return;
    const key = `${Date.now()}`;
    setUploads((u) => [...u, { key, uri: photo.uri }]);
    try {
      await upload.mutateAsync(photo);
    } catch (e) {
      Alert.alert('Photo upload failed', describeError(e as Error));
    } finally {
      setUploads((u) => u.filter((x) => x.key !== key));
    }
  };

  const confirmRemove = (p: HikePhoto) =>
    Alert.alert('Remove photo?', undefined, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Remove',
        style: 'destructive',
        onPress: () => {
          setRemoving(p.id);
          remove.mutate(p.id, { onError: (e) => Alert.alert('Couldn’t remove photo', describeError(e)), onSettled: () => setRemoving(null) });
        },
      },
    ]);

  return (
    <Section
      title="Photos"
      action={
        <ThemedText type="small" themeColor="textSecondary">
          {count}/{MAX_HIKE_PHOTOS}
        </ThemedText>
      }>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.photoStrip}>
        {photos.map((p) => (
          <Pressable key={p.id} onPress={() => setOpen(p)} accessibilityLabel={p.caption ?? 'Hike photo'} style={styles.tile}>
            <Image source={{ uri: p.url }} style={styles.tileImage} contentFit="cover" transition={150} />
            {removing === p.id ? (
              <View style={styles.tileOverlay}>
                <ActivityIndicator color="#fff" />
              </View>
            ) : (
              <Pressable accessibilityRole="button" accessibilityLabel="Remove photo" hitSlop={8} onPress={() => confirmRemove(p)} style={styles.removeButton}>
                <Icon name="xmark" size={11} color="#fff" weight="bold" />
              </Pressable>
            )}
          </Pressable>
        ))}
        {uploads.map((u) => (
          <View key={u.key} style={styles.tile}>
            <Image source={{ uri: u.uri }} style={styles.tileImage} contentFit="cover" />
            <View style={styles.tileOverlay}>
              <ActivityIndicator color="#fff" />
              <ThemedText type="caption" style={{ color: '#fff' }}>
                Uploading…
              </ThemedText>
            </View>
          </View>
        ))}
        {count < MAX_HIKE_PHOTOS && (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Add photo"
            onPress={add}
            style={({ pressed }) => [styles.tile, styles.addTile, { borderColor: theme.separator, backgroundColor: theme.backgroundElement }, pressed && { opacity: 0.7 }]}>
            <Icon name="camera.fill" size={22} color={theme.accent} />
            <ThemedText type="caption" style={{ color: theme.accent, fontWeight: 700 }}>
              Add photo
            </ThemedText>
          </Pressable>
        )}
      </ScrollView>
      <PhotoViewer photo={open} onClose={() => setOpen(null)} />
    </Section>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  caption: { minHeight: 88, textAlignVertical: 'top' },
  buttons: { gap: Spacing.two + 2, marginTop: Spacing.two },
  photoStrip: { gap: Spacing.two },
  tile: { width: 104, height: 104, borderRadius: Radius.medium, borderCurve: 'continuous', overflow: 'hidden' },
  tileImage: { width: '100%', height: '100%', backgroundColor: '#ddd' },
  tileOverlay: { ...StyleSheet.absoluteFill, backgroundColor: 'rgba(0,0,0,0.45)', alignItems: 'center', justifyContent: 'center', gap: 4 },
  removeButton: { position: 'absolute', top: 6, right: 6, width: 22, height: 22, borderRadius: 11, backgroundColor: 'rgba(0,0,0,0.6)', alignItems: 'center', justifyContent: 'center' },
  addTile: { alignItems: 'center', justifyContent: 'center', gap: 6, borderWidth: 1.5, borderStyle: 'dashed' },
});
