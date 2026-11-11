import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable, StyleSheet, View } from 'react-native';

import { useContentModerationQueue, useModerateContent } from '@/api/feed';
import { useModerateTrail, useModerateTrailPhoto, useModerationQueue, usePhotoModerationQueue } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { Badge, Button, Card, QueryState, Screen, Segmented } from '@/components/ui';
import { Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { confirmDelete } from '@/lib/action-sheet';
import { describeError } from '@/lib/api';
import type { ContentHiddenBy, ContentModerationItem, ContentReport, ModerationAction, ModerationItem, PhotoModerationItem } from '@/lib/types';

type Tab = 'trails' | 'photos' | 'comments';

const REASON_LABELS: Record<string, string> = {
  Inaccurate: 'Inaccurate',
  Dangerous: 'Dangerous',
  Inappropriate: 'Inappropriate',
  Spam: 'Spam',
  Harassment: 'Harassment',
  Other: 'Other',
};

const AMBER = '#B45309';
const GREEN = '#1F7A4D';

/** Moderators: reported or hidden trails, photos (trail and hike) and hike comments, with restore / hide / delete. */
export default function ModerationScreen() {
  const [tab, setTab] = useState<Tab>('trails');
  const trails = useModerationQueue();
  const trailPhotos = usePhotoModerationQueue();
  const content = useContentModerationQueue();

  const hikePhotos = content.data?.filter((i) => i.kind === 'HikePhoto') ?? [];
  const comments = content.data?.filter((i) => i.kind === 'Comment') ?? [];
  const photoCount = (trailPhotos.data?.length ?? 0) + hikePhotos.length;
  const label = (name: string, count: number | undefined) => (count ? `${name} · ${count}` : name);

  const refreshing = trails.isRefetching || trailPhotos.isRefetching || content.isRefetching;
  const refresh = () => {
    void trails.refetch();
    void trailPhotos.refetch();
    void content.refetch();
  };

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Segmented
        value={tab}
        onChange={setTab}
        options={[
          { value: 'trails', label: label('Trails', trails.data?.length) },
          { value: 'photos', label: label('Photos', photoCount) },
          { value: 'comments', label: label('Comments', comments.length) },
        ]}
      />

      {tab === 'trails' && (
        <>
          <QueryState
            isLoading={trails.isLoading}
            error={trails.error}
            hasData={!!trails.data?.length}
            empty={trails.data?.length === 0}
            emptyText="Nothing to review. Reported trails show up here."
            emptyIcon="checkmark.shield"
            onRetry={() => trails.refetch()}
          />
          {trails.data?.map((item) => <TrailModerationCard key={item.trailId} item={item} />)}
        </>
      )}

      {tab === 'photos' && (
        <>
          <QueryState
            isLoading={trailPhotos.isLoading || content.isLoading}
            error={trailPhotos.error ?? content.error}
            hasData={photoCount > 0}
            empty={trailPhotos.isSuccess && content.isSuccess && photoCount === 0}
            emptyText="No photos to review. Reported trail and hike photos show up here."
            emptyIcon="photo.on.rectangle"
            onRetry={refresh}
          />
          {trailPhotos.data?.map((item) => <TrailPhotoCard key={item.photoId} item={item} />)}
          {hikePhotos.map((item) => (
            <ContentCard key={item.contentId} item={item} />
          ))}
        </>
      )}

      {tab === 'comments' && (
        <>
          <QueryState
            isLoading={content.isLoading}
            error={content.error}
            hasData={comments.length > 0}
            empty={content.isSuccess && comments.length === 0}
            emptyText="No comments to review. Reported hike comments show up here."
            emptyIcon="bubble.left"
            onRetry={() => content.refetch()}
          />
          {comments.map((item) => (
            <ContentCard key={item.contentId} item={item} />
          ))}
        </>
      )}
    </Screen>
  );
}

function TrailModerationCard({ item }: { item: ModerationItem }) {
  const moderate = useModerateTrail();
  const set = (hidden: boolean) => moderate.mutate({ id: item.trailId, hidden }, { onError: (e) => Alert.alert("Couldn't update", describeError(e)) });
  return (
    <Card>
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <ThemedText type="headline" onPress={() => router.push(`/trail/${item.trailId}`)}>
            {item.name}
          </ThemedText>
          <ThemedText type="small" themeColor="textSecondary">
            {item.region}
          </ThemedText>
        </View>
        {item.hiddenBy ? <Badge label={item.hiddenBy === 'Reports' ? 'Auto-hidden' : 'Hidden'} color={AMBER} icon="eye.slash" /> : <Badge label="Live" color={GREEN} />}
      </View>
      <ReportList reports={item.reports} open={item.openReports} />
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="secondary" icon="eye" label={item.hiddenBy ? 'Restore' : 'Keep (dismiss)'} onPress={() => set(false)} loading={moderate.isPending} />
        </View>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="danger" icon="eye.slash" label="Hide" onPress={() => set(true)} disabled={item.hiddenBy === 'Moderator'} />
        </View>
      </View>
    </Card>
  );
}

function TrailPhotoCard({ item }: { item: PhotoModerationItem }) {
  const moderate = useModerateTrailPhoto();
  return (
    <ItemCard
      thumbnail={item.url}
      title={item.trailName}
      subtitle={item.caption ? `Trail photo · “${item.caption}”` : 'Trail photo'}
      onTitlePress={() => router.push(`/trail/${item.trailId}`)}
      hiddenBy={item.hiddenBy}
      reports={item.reports}
      openReports={item.openReports}
      pending={moderate.isPending ? moderate.variables?.action : undefined}
      onAction={(action) => moderate.mutate({ trailId: item.trailId, photoId: item.photoId, action }, { onError: (e) => Alert.alert("Couldn't update", describeError(e)) })}
      deleteText="The photo and its file are removed from the trail. This can’t be undone."
    />
  );
}

function ContentCard({ item }: { item: ContentModerationItem }) {
  const moderate = useModerateContent();
  const isPhoto = item.kind === 'HikePhoto';
  return (
    <ItemCard
      thumbnail={isPhoto ? item.url : null}
      title={item.authorName}
      subtitle={isPhoto ? 'Hike photo' : 'Comment on a hike'}
      body={item.text}
      onTitlePress={() => router.push(`/feed/${item.activityId}`)}
      hiddenBy={item.hiddenBy}
      reports={item.reports}
      openReports={item.openReports}
      pending={moderate.isPending ? moderate.variables?.action : undefined}
      onAction={(action) =>
        moderate.mutate({ kind: isPhoto ? 'photos' : 'comments', contentId: item.contentId, action }, { onError: (e) => Alert.alert("Couldn't update", describeError(e)) })
      }
      deleteText={isPhoto ? 'The photo and its file are removed from the hike. This can’t be undone.' : 'The comment is removed from the hike. This can’t be undone.'}
    />
  );
}

/** One reported or hidden photo / comment: what it is, its reports, and Restore / Hide / Delete. */
function ItemCard({
  thumbnail,
  title,
  subtitle,
  body,
  onTitlePress,
  hiddenBy,
  reports,
  openReports,
  pending,
  onAction,
  deleteText,
}: {
  thumbnail?: string | null;
  title: string;
  subtitle: string;
  body?: string | null;
  onTitlePress: () => void;
  hiddenBy: ContentHiddenBy;
  reports: ContentReport[];
  openReports: number;
  pending?: ModerationAction;
  onAction: (action: ModerationAction) => void;
  deleteText: string;
}) {
  const theme = useTheme();
  const busy = !!pending;
  return (
    <Card>
      <View style={styles.row}>
        {thumbnail && (
          <Pressable onPress={onTitlePress} accessibilityRole="imagebutton" accessibilityLabel="Open where it was posted">
            <Image source={{ uri: thumbnail }} style={[styles.thumb, { backgroundColor: theme.backgroundSelected }]} contentFit="cover" transition={150} />
          </Pressable>
        )}
        <View style={{ flex: 1, gap: 2 }}>
          <ThemedText type="headline" numberOfLines={1} onPress={onTitlePress}>
            {title}
          </ThemedText>
          <ThemedText type="small" themeColor="textSecondary" numberOfLines={2}>
            {subtitle}
          </ThemedText>
          <View style={{ marginTop: 4 }}>
            {hiddenBy ? <Badge label={hiddenBy === 'Reports' ? 'Auto-hidden' : 'Hidden'} color={AMBER} icon="eye.slash" /> : <Badge label="Live" color={GREEN} />}
          </View>
        </View>
      </View>
      {body != null && (
        <View style={[styles.quote, { backgroundColor: theme.backgroundSelected }]}>
          <ThemedText type="small">{body}</ThemedText>
        </View>
      )}
      <ReportList reports={reports} open={openReports} />
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="secondary" icon="eye" label={hiddenBy ? 'Restore' : 'Keep'} onPress={() => onAction('Restore')} loading={pending === 'Restore'} disabled={busy} />
        </View>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="secondary" icon="eye.slash" label="Hide" onPress={() => onAction('Hide')} loading={pending === 'Hide'} disabled={busy || hiddenBy === 'Moderator'} />
        </View>
        <View style={{ flex: 1 }}>
          <Button size="small" variant="danger" icon="trash" label="Delete" onPress={() => confirmDelete('Delete for good?', deleteText, () => onAction('Delete'))} loading={pending === 'Delete'} disabled={busy} />
        </View>
      </View>
    </Card>
  );
}

function ReportList({ reports, open }: { reports: { reason: string; details: string | null; createdAtUtc: string }[]; open: number }) {
  if (reports.length === 0) {
    return (
      <ThemedText type="small" themeColor="textSecondary">
        No open reports.
      </ThemedText>
    );
  }
  return (
    <View style={{ gap: 2 }}>
      <ThemedText type="caption" themeColor="textSecondary" style={{ fontWeight: 700 }}>
        {open === 1 ? '1 OPEN REPORT' : `${open} OPEN REPORTS`}
      </ThemedText>
      {reports.map((r, i) => (
        <ThemedText key={i} type="small">
          • {REASON_LABELS[r.reason] ?? r.reason}
          {r.details ? `: ${r.details}` : ''}
          <ThemedText type="small" themeColor="textSecondary">
            {'  '}
            {new Date(r.createdAtUtc).toLocaleDateString()}
          </ThemedText>
        </ThemedText>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  thumb: { width: 72, height: 72, borderRadius: Radius.small, borderCurve: 'continuous' },
  quote: { borderRadius: Radius.small, borderCurve: 'continuous', paddingHorizontal: Spacing.three, paddingVertical: Spacing.two },
});
