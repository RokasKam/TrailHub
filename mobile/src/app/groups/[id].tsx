import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, Share, StyleSheet, View } from 'react-native';

import { useGroupLiveLocation } from '@/api/live-location';
import { useGroup, useLeaveGroup, useLiveLocations, useRsvp, useScheduleGroupHike } from '@/api/planning';
import { useProfile } from '@/api/profile';
import { useSession } from '@/auth/session';
import { DateField } from '@/components/date-field';
import { ThemedText } from '@/components/themed-text';
import { TrailMap } from '@/components/trail-map';
import { TrailPicker } from '@/components/trail-picker';
import { Accent, Button, Card, Chip, Input, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import type { GroupHike, RsvpStatus, TrailSummary } from '@/lib/types';
import { startGroupSharing, stopGroupSharing, useGroupSharingStore } from '@/location/group-sharing';

const RSVP_LABELS: Record<RsvpStatus, string> = { Going: 'Going', Maybe: 'Maybe', NotGoing: "Can't" };

export default function GroupScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { user } = useSession();
  const group = useGroup(id);
  const locations = useLiveLocations(id);
  const live = useGroupLiveLocation(id); // receives others' updates over SignalR
  const leave = useLeaveGroup(id);
  const allowSharing = useProfile().data?.preferences.shareLocationWithGroups ?? false;
  const sharing = useGroupSharingStore((s) => s.groupIds.includes(id));
  const [busy, setBusy] = useState(false);

  if (!group.data) return <Screen><QueryState isLoading={group.isLoading} error={group.error} /></Screen>;
  const g = group.data;
  const names = new Map(g.members.map((m) => [m.userId, m.displayName]));
  const others = (locations.data ?? []).filter((l) => l.userId !== user?.id);
  const meetingPins = g.upcomingHikes
    .filter((h) => h.meetingLatitude != null && h.meetingLongitude != null)
    .map((h) => ({ id: `meet-${h.id}`, latitude: h.meetingLatitude!, longitude: h.meetingLongitude!, label: `Meet: ${h.title}`, color: Accent }));

  const toggleSharing = async () => {
    setBusy(true);
    try {
      if (sharing) {
        await stopGroupSharing(id);
        return;
      }
      if (!allowSharing) return Alert.alert('Location sharing is off', 'Turn on "Share my location with groups" in Profile → Preferences.');
      const result = await startGroupSharing(id);
      if (!result.ok) return Alert.alert('Location needed', 'Allow location access to share where you are with your group.');
      if (!result.background) Alert.alert('Sharing while the app is open', 'Allow "Always" location access in Settings to keep sharing with the screen off.');
    } catch (e) {
      Alert.alert('Location unavailable', (e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <Stack.Screen options={{ title: g.name }} />

      <Section title={`Live map · ${live.state}`}>
        <TrailMap
          showUser
          center={others[0] ?? meetingPins[0]}
          zoom={13}
          markers={[
            ...others.map((l) => ({ id: l.userId, latitude: l.latitude, longitude: l.longitude, label: names.get(l.userId) ?? 'Member', color: '#2563EB' })),
            ...meetingPins,
          ]}
        />
        <ThemedText type="small" themeColor="textSecondary">
          {others.length === 0 ? 'Nobody else is sharing right now.' : `${others.length} member${others.length > 1 ? 's' : ''} sharing`}
          {sharing ? ' · You are sharing (also in the background)' : ''}
        </ThemedText>
        <Button label={sharing ? 'Stop sharing my location' : 'Share my location'} variant={sharing ? 'danger' : 'primary'} onPress={toggleSharing} loading={busy} />
      </Section>

      {g.inviteCode && (
        <Card>
          <ThemedText type="small">Invite code</ThemedText>
          <ThemedText type="subtitle" selectable>
            {g.inviteCode}
          </ThemedText>
          <Button label="Share invite" variant="secondary" onPress={() => Share.share({ message: `Join "${g.name}" on TrailHub with code ${g.inviteCode}` })} />
        </Card>
      )}

      <Section title="Upcoming hikes">
        {g.upcomingHikes.length === 0 && <ThemedText type="small" themeColor="textSecondary">No hikes scheduled.</ThemedText>}
        {g.upcomingHikes.map((h) => (
          <HikeCard key={h.id} groupId={id} hike={h} names={names} myId={user?.id} />
        ))}
        <ScheduleHikeForm groupId={id} />
      </Section>

      <Section title={`Members · ${g.members.length}`}>
        {g.members.map((m) => (
          <View key={m.userId} style={styles.member}>
            <ThemedText type="small">{m.displayName}</ThemedText>
            <ThemedText type="small" themeColor="textSecondary">
              {m.role}
            </ThemedText>
          </View>
        ))}
      </Section>

      <Button
        label="Leave group"
        variant="danger"
        onPress={() =>
          leave.mutate(undefined, {
            onSuccess: () => {
              void stopGroupSharing(id);
              router.back();
            },
            onError: (e) => Alert.alert("Can't leave", e.message),
          })
        }
      />
    </Screen>
  );
}

function HikeCard({ groupId, hike, names, myId }: { groupId: string; hike: GroupHike; names: Map<string, string>; myId?: string }) {
  const rsvp = useRsvp(groupId);
  const mine = hike.rsvps.find((r) => r.userId === myId)?.status;
  const going = hike.rsvps.filter((r) => r.status === 'Going').map((r) => names.get(r.userId) ?? 'Former member');
  const maybe = hike.rsvps.filter((r) => r.status === 'Maybe').length;

  return (
    <Card>
      <ThemedText type="smallBold">{hike.title}</ThemedText>
      <ThemedText type="small" themeColor="textSecondary">
        {new Date(hike.scheduledAtUtc).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
        {hike.meetingLatitude != null && ' · meeting point on the map'}
      </ThemedText>
      {hike.notes && <ThemedText type="small">{hike.notes}</ThemedText>}
      <ThemedText type="small">
        ✅ {going.length ? going.join(', ') : 'Nobody yet'}
        {maybe > 0 && ` · ${maybe} maybe`}
      </ThemedText>
      <View style={styles.row}>
        {(Object.keys(RSVP_LABELS) as RsvpStatus[]).map((status) => (
          <Chip
            key={status}
            label={RSVP_LABELS[status]}
            selected={mine === status}
            onPress={() => rsvp.mutate({ hikeId: hike.id, status }, { onError: (e) => Alert.alert('Could not answer', e.message) })}
          />
        ))}
      </View>
      {hike.trailId && <Button label="View trail" variant="secondary" onPress={() => router.push(`/trail/${hike.trailId}`)} />}
    </Card>
  );
}

function ScheduleHikeForm({ groupId }: { groupId: string }) {
  const schedule = useScheduleGroupHike(groupId);
  const [now] = useState(() => new Date());
  const [title, setTitle] = useState('');
  const [when, setWhen] = useState(() => {
    // Default: next Saturday 08:00
    const d = new Date();
    d.setDate(d.getDate() + ((6 - d.getDay() + 7) % 7 || 7));
    d.setHours(8, 0, 0, 0);
    return d;
  });
  const [trail, setTrail] = useState<TrailSummary | null>(null);
  const [meeting, setMeeting] = useState<{ latitude: number; longitude: number } | null>(null);
  const [notes, setNotes] = useState('');
  const [picking, setPicking] = useState(false);

  const reset = () => {
    setTitle('');
    setTrail(null);
    setMeeting(null);
    setNotes('');
  };

  return (
    <Card>
      <ThemedText type="smallBold">Schedule a hike</ThemedText>
      <Input placeholder="Title, e.g. Sunrise on the ridge" value={title} onChangeText={setTitle} />
      <DateField label="When" mode="datetime" value={when} onChange={setWhen} minimumDate={now} />
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button label={trail ? trail.name : 'Choose trail (optional)'} variant="secondary" onPress={() => setPicking(true)} />
        </View>
        {trail && <Button label="✕" variant="secondary" onPress={() => { setTrail(null); setMeeting(null); }} />}
      </View>
      {(trail || meeting) && (
        <>
          <ThemedText type="small" themeColor="textSecondary">
            Meeting point{meeting === null ? ': none' : ''} · tap the map to move it
          </ThemedText>
          <TrailMap
            height={180}
            center={meeting ?? trail?.start}
            zoom={13}
            markers={meeting ? [{ id: 'meeting', label: 'Meet here', color: Accent, ...meeting }] : []}
            onMapPress={setMeeting}
          />
        </>
      )}
      <Input placeholder="Notes (optional)" value={notes} onChangeText={setNotes} multiline />
      {schedule.error && <ThemedText type="small">{schedule.error.message}</ThemedText>}
      <Button
        label="Schedule hike"
        disabled={!title.trim() || when <= now}
        loading={schedule.isPending}
        onPress={() =>
          schedule.mutate(
            {
              title,
              trailId: trail?.id ?? null,
              scheduledAtUtc: when.toISOString(),
              meetingLatitude: meeting?.latitude ?? null,
              meetingLongitude: meeting?.longitude ?? null,
              notes: notes.trim() || null,
            },
            { onSuccess: reset },
          )
        }
      />
      <TrailPicker
        visible={picking}
        title="Trail for this hike"
        onClose={() => setPicking(false)}
        onSelect={(t) => {
          setTrail(t);
          setMeeting(t.start); // trailhead is the usual meeting point
          setPicking(false);
        }}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  member: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: Spacing.one },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two, alignItems: 'center' },
});
