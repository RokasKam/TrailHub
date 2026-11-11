import { type InfiniteData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ContentModerationItem, ContentReportReason, FeedComment, FeedPost, FeedPostDetails, LikeState, ModerationAction, Paged } from '@/lib/types';

// Social data changes often and isn't needed offline, so none of it is persisted to disk.
export const feedKeys = {
  all: ['feed'] as const,
  lists: ['feed', 'list'] as const,
  list: (groupId: string | null) => ['feed', 'list', groupId ?? 'all'] as const,
  post: (id: string) => ['feed', 'post', id] as const,
};

/** Reported or hidden hike comments and photos. Kept outside `feed` so feed refreshes don't refetch it. */
export const contentModerationKey = ['activities', 'moderation'] as const;

/** Hikes shared with the caller's groups, newest first; optionally one group only. */
export function useFeed(groupId: string | null) {
  return useInfiniteQuery({
    queryKey: feedKeys.list(groupId),
    queryFn: ({ pageParam, signal }) => api<Paged<FeedPost>>('/api/activities/feed', { query: { groupId, page: pageParam, pageSize: 20 }, signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    meta: { persist: false },
  });
}

export function useFeedPost(id: string) {
  return useQuery({
    queryKey: feedKeys.post(id),
    queryFn: ({ signal }) => api<FeedPostDetails>(`/api/activities/feed/${id}`, { signal }),
    meta: { persist: false },
  });
}

/** Applies a change to a post wherever it's cached: its detail page and every feed list it appears in. */
function patchPost(qc: ReturnType<typeof useQueryClient>, id: string, patch: (post: FeedPost) => Partial<FeedPost>) {
  qc.setQueryData<FeedPostDetails>(feedKeys.post(id), (p) => (p ? { ...p, ...patch(p) } : p));
  qc.setQueriesData<InfiniteData<Paged<FeedPost>>>({ queryKey: feedKeys.lists }, (data) =>
    data
      ? { ...data, pages: data.pages.map((page) => ({ ...page, items: page.items.map((p) => (p.activityId === id ? { ...p, ...patch(p) } : p)) })) }
      : data,
  );
}

/** Optimistic like/unlike: the heart flips at once and rolls back if the request fails. */
export function useToggleLike(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (like: boolean) => api<LikeState>(`/api/activities/feed/${id}/like`, { method: like ? 'PUT' : 'DELETE' }),
    onMutate: async (like) => {
      await qc.cancelQueries({ queryKey: feedKeys.all });
      patchPost(qc, id, (p) => (p.likedByMe === like ? {} : { likedByMe: like, likeCount: Math.max(0, p.likeCount + (like ? 1 : -1)) }));
      return { like };
    },
    onSuccess: (state) => patchPost(qc, id, () => state),
    onError: (_e, like) => patchPost(qc, id, (p) => (p.likedByMe !== like ? {} : { likedByMe: !like, likeCount: Math.max(0, p.likeCount + (like ? -1 : 1)) })),
  });
}

export function useAddComment(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => api<FeedComment>(`/api/activities/feed/${id}/comments`, { method: 'POST', body: { text } }),
    onSuccess: (comment) => {
      qc.setQueryData<FeedPostDetails>(feedKeys.post(id), (p) => (p ? { ...p, comments: [...p.comments, comment], commentCount: p.commentCount + 1 } : p));
      void qc.invalidateQueries({ queryKey: feedKeys.all });
    },
  });
}

export function useDeleteComment(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (commentId: string) => api<void>(`/api/activities/feed/${id}/comments/${commentId}`, { method: 'DELETE' }),
    onSuccess: (_r, commentId) => {
      qc.setQueryData<FeedPostDetails>(feedKeys.post(id), (p) =>
        p ? { ...p, comments: p.comments.filter((c) => c.id !== commentId), commentCount: Math.max(0, p.commentCount - 1) } : p,
      );
      void qc.invalidateQueries({ queryKey: feedKeys.all });
    },
  });
}

type ReportBody = { reason: ContentReportReason; details?: string | null };

export function useReportComment(postId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, ...body }: ReportBody & { commentId: string }) =>
      api<void>(`/api/activities/feed/${postId}/comments/${commentId}/report`, { method: 'POST', body }),
    // A third report hides the comment.
    onSuccess: () => qc.invalidateQueries({ queryKey: feedKeys.post(postId) }),
  });
}

export function useReportHikePhoto(postId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ photoId, ...body }: ReportBody & { photoId: string }) =>
      api<void>(`/api/activities/feed/${postId}/photos/${photoId}/report`, { method: 'POST', body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: feedKeys.all }),
  });
}

/** Moderators only; 403 for everyone else, which is how the app knows. */
export function useContentModerationQueue() {
  return useQuery({
    queryKey: contentModerationKey,
    queryFn: ({ signal }) => api<ContentModerationItem[]>('/api/activities/moderation', { signal }),
    retry: false,
    meta: { persist: false },
  });
}

export type ModerateContentInput = { kind: 'comments' | 'photos'; contentId: string; action: ModerationAction };

/** Restore / hide / delete a hike comment or photo (moderators). Also how moderators delete from the feed. */
export function useModerateContent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ kind, contentId, action }: ModerateContentInput) => api<void>(`/api/activities/moderation/${kind}/${contentId}`, { method: 'PUT', body: { action } }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: contentModerationKey });
      void qc.invalidateQueries({ queryKey: feedKeys.all });
      void qc.invalidateQueries({ queryKey: ['activities'] });
    },
  });
}
