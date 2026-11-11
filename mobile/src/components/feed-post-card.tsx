import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useMemo } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { useToggleLike } from '@/api/feed';
import { useUnits } from '@/api/profile';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Card, Icon, Stat, StatRow } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { formatDistance, formatDuration, formatElevation, formatTimeAgo } from '@/lib/format';
import { boundsOf } from '@/lib/geo';
import type { FeedPost, TrackPrivacy } from '@/lib/types';

const LIKE_RED = '#E11D48';

/** The API sends the track as [lat, lon] pairs. */
export function useTrackLine(track: [number, number][]) {
  return useMemo(() => {
    const line = track.map(([latitude, longitude]) => ({ latitude, longitude }));
    return { line, bounds: boundsOf(line) };
  }, [track]);
}

export const PRIVACY_LABELS: Record<TrackPrivacy, string> = { Full: 'Full map', HideEnds: 'Hide start & end', StatsOnly: 'Stats only' };

/** Round badge with the author's initials. */
export function Avatar({ name, size = 36 }: { name: string; size?: number }) {
  const theme = useTheme();
  const initials =
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase())
      .join('') || '?';
  return (
    <View style={{ width: size, height: size, borderRadius: size / 2, backgroundColor: theme.accentSoft, alignItems: 'center', justifyContent: 'center' }}>
      <ThemedText type="caption" style={{ color: theme.accent, fontWeight: 800, fontSize: size * 0.36, lineHeight: size * 0.44 }}>
        {initials}
      </ThemedText>
    </View>
  );
}

/** Author, when it was shared and with which groups. */
export function PostByline({ post }: { post: FeedPost }) {
  return (
    <View style={styles.byline}>
      <Avatar name={post.author.displayName} />
      <View style={{ flex: 1 }}>
        <ThemedText type="smallBold" numberOfLines={1}>
          {post.isMine ? `${post.author.displayName} (you)` : post.author.displayName}
        </ThemedText>
        <ThemedText type="caption" themeColor="textSecondary" numberOfLines={1}>
          {formatTimeAgo(post.sharedAtUtc)}
          {post.groups.length > 0 && ` · ${post.groups.map((g) => g.name).join(', ')}`}
        </ThemedText>
      </View>
    </View>
  );
}

/** Heart + count; flips immediately (optimistic). */
export function LikeButton({ post }: { post: FeedPost }) {
  const theme = useTheme();
  const toggle = useToggleLike(post.activityId);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={post.likedByMe ? 'Unlike' : 'Like'}
      accessibilityState={{ selected: post.likedByMe }}
      hitSlop={8}
      onPress={() => toggle.mutate(!post.likedByMe)}
      style={({ pressed }) => [styles.action, { transform: [{ scale: pressed ? 0.92 : 1 }] }]}>
      <Icon name={post.likedByMe ? 'heart.fill' : 'heart'} size={19} color={post.likedByMe ? LIKE_RED : theme.textSecondary} />
      <ThemedText type="smallBold" style={{ color: post.likedByMe ? LIKE_RED : theme.textSecondary }}>
        {post.likeCount}
      </ThemedText>
    </Pressable>
  );
}

/** A hike in the feed. Tapping anywhere but the like button opens the post. */
export function FeedPostCard({ post }: { post: FeedPost }) {
  const theme = useTheme();
  const units = useUnits();
  const { line, bounds } = useTrackLine(post.track);
  const open = () => router.push(`/feed/${post.activityId}`);
  const photos = post.photos.slice(0, 3);

  return (
    <Card style={styles.card}>
      <Pressable onPress={open} accessibilityRole="button" accessibilityLabel={`${post.name} by ${post.author.displayName}`} style={({ pressed }) => [styles.body, pressed && { opacity: 0.75 }]}>
        <PostByline post={post} />
        <View style={{ gap: 2 }}>
          <ThemedText type="headline" numberOfLines={2}>
            {post.name}
          </ThemedText>
          {post.caption && (
            <ThemedText numberOfLines={3}>
              {post.caption}
            </ThemedText>
          )}
        </View>

        {line.length >= 2 && (
          // The preview isn't interactive: drags scroll the feed, taps open the post.
          <View style={{ pointerEvents: 'none' }}>
            <TrailMap height={170} lines={[line]} bounds={bounds} />
          </View>
        )}

        <StatRow>
          <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(post.distanceMeters, units)} />
          <Stat icon="arrow.up.forward" label="Gain" value={formatElevation(post.elevationGainMeters, units)} />
          <Stat icon="timer" label="Moving" value={formatDuration(post.movingDurationSeconds)} />
        </StatRow>

        {photos.length > 0 && (
          <View style={styles.photos}>
            {photos.map((p, i) => (
              <View key={p.id} style={styles.photoCell}>
                <Image source={{ uri: p.url }} style={styles.photo} contentFit="cover" transition={150} />
                {i === photos.length - 1 && post.photos.length > photos.length && (
                  <View style={styles.more}>
                    <ThemedText type="smallBold" style={{ color: '#fff' }}>
                      +{post.photos.length - photos.length}
                    </ThemedText>
                  </View>
                )}
              </View>
            ))}
          </View>
        )}
      </Pressable>

      <View style={[styles.actions, { borderTopColor: theme.separator }]}>
        <LikeButton post={post} />
        <Pressable accessibilityRole="button" accessibilityLabel={`${post.commentCount} comments`} hitSlop={8} onPress={open} style={styles.action}>
          <Icon name="bubble.left" size={18} color={theme.textSecondary} />
          <ThemedText type="smallBold" themeColor="textSecondary">
            {post.commentCount}
          </ThemedText>
        </Pressable>
        <View style={{ flex: 1 }} />
        {post.privacy === 'StatsOnly' && (
          <View style={styles.action}>
            <Icon name="eye.slash" size={13} color={theme.textSecondary} />
            <ThemedText type="caption" themeColor="textSecondary">
              Map hidden
            </ThemedText>
          </View>
        )}
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  card: { gap: Spacing.three },
  body: { gap: Spacing.three },
  byline: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two + 2 },
  photos: { flexDirection: 'row', gap: Spacing.two },
  photoCell: { flex: 1, aspectRatio: 1, borderRadius: Radius.small, borderCurve: 'continuous', overflow: 'hidden', backgroundColor: '#ddd' },
  photo: { width: '100%', height: '100%' },
  more: { ...StyleSheet.absoluteFill, backgroundColor: 'rgba(0,0,0,0.45)', alignItems: 'center', justifyContent: 'center' },
  actions: { flexDirection: 'row', alignItems: 'center', gap: Spacing.four, borderTopWidth: StyleSheet.hairlineWidth, paddingTop: Spacing.two + 2 },
  action: { flexDirection: 'row', alignItems: 'center', gap: 6, minHeight: 28 },
});
