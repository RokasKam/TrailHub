import { router } from 'expo-router';

import { List, ListRow } from '@/components/ui';
import { formatDate } from '@/lib/format';
import type { TripSummary } from '@/lib/types';

function tripDetail(t: TripSummary) {
  const parts = [`${formatDate(t.startDate)} – ${formatDate(t.endDate)}`, `${t.dayCount} day${t.dayCount > 1 ? 's' : ''}`, `${t.trailCount} trail${t.trailCount === 1 ? '' : 's'}`];
  if (t.groupName) parts.push(t.groupName);
  if (t.sharedUnclaimed > 0) parts.push(`${t.sharedUnclaimed} gear unclaimed`);
  return parts.join(' · ');
}

export function TripList({ trips }: { trips: TripSummary[] }) {
  return (
    <List>
      {trips.map((t) => (
        <ListRow
          key={t.id}
          icon={t.status === 'Completed' ? 'checkmark.circle.fill' : t.status === 'Cancelled' ? 'xmark.circle.fill' : t.groupId ? 'person.3.fill' : 'map.fill'}
          iconColor={t.status === 'Completed' ? '#16A34A' : t.status === 'Cancelled' ? '#9CA3AF' : t.groupId ? '#2563EB' : '#EA580C'}
          title={t.name}
          detail={tripDetail(t)}
          onPress={() => router.push(`/trips/${t.id}`)}
        />
      ))}
    </List>
  );
}
