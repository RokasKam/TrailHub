import { type InfiniteData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { FeedComment, FeedPost, FeedPostDetails, LikeState, Paged } from '@/lib/types';

// Social data changes often and isn't needed offline, so none of it is persisted to disk.
export const feedKeys = {
  all: ['feed'] as const,
  lists: ['feed', 'list'] as const,
  list: (groupId: string | null) => ['feed', 'list', groupId ?? 'all'] as const,
  post: (id: string) => ['feed', 'post', id] as const,
};

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
