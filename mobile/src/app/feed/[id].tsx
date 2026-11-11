import { router, Stack, useLocalSearchParams } from 'expo-router';
import { type ComponentRef, useRef, useState } from 'react';
import { ActivityIndicator, Alert, KeyboardAvoidingView, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAddComment, useDeleteComment, useFeedPost } from '@/api/feed';
import { useUnits } from '@/api/profile';
import { ElevationChart } from '@/components/charts';
import { Avatar, LikeButton, PostByline, useTrackLine } from '@/components/feed-post-card';
import { HikePhotoStrip } from '@/components/hike-photos';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { Badge, Button, Card, EmptyState, Icon, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { ApiError, describeError } from '@/lib/api';
import { formatDate, formatDistance, formatDuration, formatElevation, formatTimeAgo } from '@/lib/format';
import type { FeedComment, FeedPostDetails } from '@/lib/types';

const MAX_COMMENT = 1000;

/** A shared hike as group members see it: privacy-applied map, stats, photos, likes and comments. */
export default function FeedPostScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const post = useFeedPost(id);

  if (!post.data) {
    // 404: unshared, or you left the group it was shared with.
    if (post.error instanceof ApiError && post.error.status === 404) {
      return (
        <Screen>
          <Stack.Screen options={{ title: '' }} />
          <Card style={{ alignItems: 'center' }}>
            <EmptyState icon="eye.slash" title="This hike isn’t shared with you" message="The author may have stopped sharing it, or you’re no longer in the group." />
            <Button label="Back" variant="tinted" size="small" onPress={() => router.back()} />
          </Card>
        </Screen>
      );
    }
    return (
      <Screen>
        <Stack.Screen options={{ title: '' }} />
        <QueryState isLoading={post.isLoading} error={post.error} onRetry={() => post.refetch()} />
      </Screen>
    );
  }
  return <PostView post={post.data} refreshing={post.isRefetching} onRefresh={() => void post.refetch()} />;
}

function PostView({ post, refreshing, onRefresh }: { post: FeedPostDetails; refreshing: boolean; onRefresh: () => void }) {
  const theme = useTheme();
  const units = useUnits();
  const { line, bounds } = useTrackLine(post.track);
  // KeyboardAvoidingView measures itself relative to its parent; offset by where that parent sits on screen
  // (below an opaque header, or at 0 under a translucent one) so the composer lands right above the keyboard.
  const [offset, setOffset] = useState(0);
  const wrapper = useRef<ComponentRef<typeof View>>(null);

  return (
    <View ref={wrapper} style={{ flex: 1, backgroundColor: theme.background }} onLayout={() => wrapper.current?.measureInWindow((_x, y) => setOffset(y))}>
      <Stack.Screen options={{ title: '' }} />
      <KeyboardAvoidingView style={{ flex: 1 }} behavior="padding" keyboardVerticalOffset={offset}>
        <Screen refreshing={refreshing} onRefresh={onRefresh}>
          <View style={styles.bylineRow}>
            <View style={{ flex: 1 }}>
              <PostByline post={post} />
            </View>
            {post.isMine && <Button label="Edit sharing" variant="tinted" icon="pencil" size="small" onPress={() => router.push(`/activity/share?id=${post.activityId}`)} />}
          </View>

          {line.length >= 2 ? (
            <TrailMap height={300} lines={[line]} bounds={bounds} />
          ) : (
            post.privacy === 'StatsOnly' && (
              <Card flat style={styles.hiddenMap}>
                <Icon name="lock.fill" size={18} color={theme.textSecondary} />
                <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
                  {post.isMine ? 'You shared stats only, so your groups don’t see a map.' : 'The map is hidden. Only stats were shared.'}
                </ThemedText>
              </Card>
            )
          )}

          <View style={{ gap: 4 }}>
            <ThemedText type="subtitle">{post.name}</ThemedText>
            <ThemedText type="small" themeColor="textSecondary">
              {formatDate(post.startedAtUtc)}
              {post.privacy === 'HideEnds' && ' · Start and end hidden'}
            </ThemedText>
          </View>
          {post.caption && <ThemedText>{post.caption}</ThemedText>}
          {post.groups.length > 0 && (
            <View style={styles.badges}>
              {post.groups.map((g) => (
                <Badge key={g.groupId} label={g.name} icon="person.2.fill" color={theme.accent} />
              ))}
            </View>
          )}

          <Card>
            <StatRow>
              <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(post.distanceMeters, units)} />
              <Stat icon="timer" label="Moving" value={formatDuration(post.movingDurationSeconds)} />
              <Stat icon="arrow.up.forward" label="Gain" value={formatElevation(post.elevationGainMeters, units)} />
            </StatRow>
          </Card>

          {post.elevationProfile.length > 1 && (
            <Section title="Elevation">
              <Card>
                <ElevationChart samples={post.elevationProfile} units={units} />
              </Card>
            </Section>
          )}

          {post.photos.length > 0 && (
            <Section title="Photos">
              <HikePhotoStrip photos={post.photos} />
            </Section>
          )}

          <View style={[styles.likeRow, { borderColor: theme.separator }]}>
            <LikeButton post={post} />
            <ThemedText type="small" themeColor="textSecondary">
              {post.likeCount === 1 ? '1 like' : `${post.likeCount} likes`} · {post.commentCount === 1 ? '1 comment' : `${post.commentCount} comments`}
            </ThemedText>
          </View>

          <Section title="Comments">
            {post.comments.length === 0 ? (
              <ThemedText type="small" themeColor="textSecondary">
                No comments yet. Say something nice.
              </ThemedText>
            ) : (
              <Card style={styles.comments}>
                {post.comments.map((c, i) => (
                  <CommentRow key={c.id} postId={post.activityId} comment={c} first={i === 0} />
                ))}
              </Card>
            )}
          </Section>
        </Screen>
        <Composer postId={post.activityId} />
      </KeyboardAvoidingView>
    </View>
  );
}

function CommentRow({ postId, comment, first }: { postId: string; comment: FeedComment; first: boolean }) {
  const theme = useTheme();
  const remove = useDeleteComment(postId);
  const confirm = () =>
    Alert.alert('Delete comment?', undefined, [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Delete', style: 'destructive', onPress: () => remove.mutate(comment.id, { onError: (e) => Alert.alert('Couldn’t delete comment', describeError(e)) }) },
    ]);
  return (
    <View style={[styles.comment, !first && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: theme.separator }]}>
      <Avatar name={comment.author.displayName} size={30} />
      <View style={{ flex: 1, gap: 2 }}>
        <View style={styles.commentHeader}>
          <ThemedText type="smallBold" numberOfLines={1} style={{ flexShrink: 1 }}>
            {comment.author.displayName}
          </ThemedText>
          <ThemedText type="caption" themeColor="textSecondary">
            {formatTimeAgo(comment.createdAtUtc)}
          </ThemedText>
        </View>
        <ThemedText type="small">{comment.text}</ThemedText>
      </View>
      {comment.canDelete &&
        (remove.isPending ? (
          <ActivityIndicator size="small" />
        ) : (
          <Pressable accessibilityRole="button" accessibilityLabel="Delete comment" hitSlop={10} onPress={confirm}>
            <Icon name="trash" size={15} color={theme.textSecondary} />
          </Pressable>
        ))}
    </View>
  );
}

/** Comment box pinned under the post; the KeyboardAvoidingView lifts it above the keyboard. */
function Composer({ postId }: { postId: string }) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const add = useAddComment(postId);
  const [text, setText] = useState('');
  const [focused, setFocused] = useState(false);
  const trimmed = text.trim();

  const send = () => {
    if (!trimmed || add.isPending) return;
    add.mutate(trimmed, { onSuccess: () => setText(''), onError: (e) => Alert.alert('Couldn’t post comment', describeError(e)) });
  };

  return (
    <View style={[styles.composer, { backgroundColor: theme.backgroundElement, borderTopColor: theme.separator, paddingBottom: (focused ? 0 : insets.bottom) + Spacing.two }]}>
      <TextInput
        value={text}
        onChangeText={setText}
        placeholder="Add a comment…"
        placeholderTextColor={theme.textSecondary}
        multiline
        maxLength={MAX_COMMENT}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        style={[styles.composerInput, { color: theme.text, backgroundColor: theme.background }]}
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Post comment"
        disabled={!trimmed || add.isPending}
        onPress={send}
        style={({ pressed }) => [styles.send, { backgroundColor: theme.accent, opacity: !trimmed ? 0.4 : 1, transform: [{ scale: pressed ? 0.92 : 1 }] }]}>
        {add.isPending ? <ActivityIndicator color="#fff" size="small" /> : <Icon name="paperplane.fill" size={16} color="#fff" />}
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  bylineRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  hiddenMap: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two + 2, paddingVertical: Spacing.four },
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  likeRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three, borderTopWidth: StyleSheet.hairlineWidth, borderBottomWidth: StyleSheet.hairlineWidth, paddingVertical: Spacing.two + 2 },
  comments: { paddingVertical: Spacing.one, gap: 0 },
  comment: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.two + 2, paddingVertical: Spacing.two + 2 },
  commentHeader: { flexDirection: 'row', alignItems: 'baseline', gap: Spacing.two },
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: Spacing.two, paddingHorizontal: Spacing.three, paddingTop: Spacing.two, borderTopWidth: StyleSheet.hairlineWidth },
  composerInput: { flex: 1, minHeight: 40, maxHeight: 120, borderRadius: Radius.medium, borderCurve: 'continuous', paddingHorizontal: Spacing.three, paddingTop: 10, paddingBottom: 10, fontSize: 16 },
  send: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
});
