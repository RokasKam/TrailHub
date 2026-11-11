import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Switch, View } from 'react-native';

import { useAchievements, useProfile, useUpdatePreferences, useUpdateProfile } from '@/api/profile';
import { useContentModerationQueue } from '@/api/feed';
import { useModerationQueue, usePhotoModerationQueue } from '@/api/trails';
import { useSession } from '@/auth/session';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Icon, IconButton, Input, List, ListRow, QueryState, Screen, Section, Segmented, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';
import { formatDistance, formatElevation } from '@/lib/format';
import type { MapStyle, Preferences, Units } from '@/lib/types';

export default function ProfileScreen() {
  const { signOut } = useSession();
  const theme = useTheme();
  const profile = useProfile();
  const achievements = useAchievements();
  const updatePreferences = useUpdatePreferences();
  // The queues answer 403 for non-moderators, so the row only shows for moderators (of either service).
  const trailQueue = useModerationQueue();
  const photoQueue = usePhotoModerationQueue();
  const contentQueue = useContentModerationQueue();
  const isModerator = trailQueue.isSuccess || photoQueue.isSuccess || contentQueue.isSuccess;
  const toReview = (trailQueue.data?.length ?? 0) + (photoQueue.data?.length ?? 0) + (contentQueue.data?.length ?? 0);
  const updateProfile = useUpdateProfile();
  const [editing, setEditing] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [homeRegion, setHomeRegion] = useState('');

  if (!profile.data) {
    return (
      <Screen tabBar>
        <QueryState isLoading={profile.isLoading} error={profile.error} onRetry={() => profile.refetch()} />
        <List>
          <ListRow title="Sign out" destructive onPress={signOut} />
        </List>
      </Screen>
    );
  }
  const p = profile.data;
  const units = p.preferences.units;
  const setPreference = (change: Partial<Preferences>) => updatePreferences.mutate({ ...p.preferences, ...change });

  const startEditing = () => {
    setDisplayName(p.displayName);
    setHomeRegion(p.homeRegion ?? '');
    setEditing(true);
  };

  // An email-as-name gives one letter, not "RG" from "rokas…@gmail".
  const nameWords = p.displayName.includes('@') ? [p.displayName] : p.displayName.split(/\s+/).filter(Boolean);
  const initials = nameWords.slice(0, 2).map((w) => w[0]?.toUpperCase()).join('');

  return (
    <Screen tabBar>
      {editing ? (
        <Card>
          <ThemedText type="headline">Edit profile</ThemedText>
          <Input icon="person" placeholder="Display name" value={displayName} onChangeText={setDisplayName} />
          <Input icon="mappin.and.ellipse" placeholder="Home region" value={homeRegion} onChangeText={setHomeRegion} />
          {updateProfile.error && <ThemedText type="small">{describeError(updateProfile.error)}</ThemedText>}
          <View style={styles.row}>
            <View style={{ flex: 1 }}>
              <Button label="Cancel" variant="secondary" onPress={() => setEditing(false)} />
            </View>
            <View style={{ flex: 1 }}>
              <Button
                label="Save"
                icon="checkmark"
                disabled={!displayName.trim()}
                loading={updateProfile.isPending}
                onPress={() => updateProfile.mutate({ displayName, homeRegion: homeRegion || null, bio: p.bio }, { onSuccess: () => setEditing(false) })}
              />
            </View>
          </View>
        </Card>
      ) : (
        <View style={styles.header}>
          <View style={[styles.avatar, { backgroundColor: theme.accent }]}>
            <ThemedText type="headline" style={styles.avatarText}>
              {initials || '?'}
            </ThemedText>
          </View>
          <View style={{ flex: 1 }}>
            <ThemedText type="subtitle" numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.6}>
              {p.displayName}
            </ThemedText>
            <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
              {[p.homeRegion, p.email !== p.displayName ? p.email : null].filter(Boolean).join(' · ') || 'Hiker'}
            </ThemedText>
          </View>
          <IconButton icon="pencil" label="Edit profile" onPress={startEditing} />
        </View>
      )}

      <Card>
        <StatRow>
          <Stat icon="figure.hiking" label="Hikes" value={String(p.totals.completedHikes)} />
          <Stat icon="point.topleft.down.to.point.bottomright.curvepath" label="Distance" value={formatDistance(p.totals.distanceMeters, units)} />
          <Stat icon="arrow.up.forward" label="Climbed" value={formatElevation(p.totals.elevationGainMeters, units)} />
        </StatRow>
      </Card>

      <List>
        <ListRow icon="map.fill" iconColor="#EA580C" title="Trips" detail="Multi-day plans and checklists" onPress={() => router.push('/trips')} />
        <ListRow icon="person.3.fill" iconColor="#2563EB" title="Groups" detail="Hike together, share your location" onPress={() => router.push('/groups')} />
        <ListRow icon="checklist" iconColor="#1F7A4D" title="Gear lists" detail="Reusable packing lists for your trips" onPress={() => router.push('/gear')} />
        {isModerator && (
          <ListRow
            icon="checkmark.shield.fill"
            iconColor="#7C3AED"
            title="Moderation"
            detail={toReview ? `${toReview} item${toReview === 1 ? '' : 's'} to review` : 'Nothing to review'}
            onPress={() => router.push('/moderation')}
          />
        )}
      </List>

      <Section
        title="Achievements"
        action={
          <ThemedText type="small" themeColor="textSecondary">
            {p.achievementCount} of {achievements.data?.length ?? '–'}
          </ThemedText>
        }>
        <View style={styles.grid}>
          {achievements.data?.map((a) => (
            <Card key={a.code} style={[styles.achievement, !a.unlocked && styles.locked]}>
              <View style={[styles.medal, { backgroundColor: a.unlocked ? '#FEF3C7' : theme.backgroundSelected }]}>
                <Icon name={a.unlocked ? 'medal.fill' : 'lock.fill'} size={20} color={a.unlocked ? '#D97706' : theme.textSecondary} />
              </View>
              <ThemedText type="smallBold">{a.title}</ThemedText>
              <ThemedText type="caption" themeColor="textSecondary">
                {a.description}
              </ThemedText>
            </Card>
          ))}
        </View>
      </Section>

      <Section title="Preferences">
        <Card>
          <ThemedText type="smallBold">Units</ThemedText>
          <Segmented<Units>
            value={units}
            onChange={(u) => setPreference({ units: u })}
            options={[
              { value: 'Metric', label: 'Metric' },
              { value: 'Imperial', label: 'Imperial' },
            ]}
          />
          <ThemedText type="smallBold" style={{ marginTop: Spacing.two }}>
            Map style
          </ThemedText>
          <Segmented<MapStyle>
            value={p.preferences.mapStyle}
            onChange={(m) => setPreference({ mapStyle: m })}
            options={[
              { value: 'Outdoors', label: 'Outdoors' },
              { value: 'Terrain', label: 'Terrain' },
              { value: 'Satellite', label: 'Satellite' },
            ]}
          />
        </Card>
        <List>
          <ListRow
            icon="location.fill"
            iconColor="#2563EB"
            title="Share location with groups"
            trailing={<Switch value={p.preferences.shareLocationWithGroups} onValueChange={(v) => setPreference({ shareLocationWithGroups: v })} trackColor={{ true: theme.accent }} />}
          />
          <ListRow
            icon="bell.fill"
            iconColor="#DC2626"
            title="Notifications"
            trailing={<Switch value={p.preferences.notificationsEnabled} onValueChange={(v) => setPreference({ notificationsEnabled: v })} trackColor={{ true: theme.accent }} />}
          />
        </List>
      </Section>

      <List>
        <ListRow title="Sign out" destructive onPress={signOut} />
      </List>
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  avatar: { width: 64, height: 64, borderRadius: 32, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: '#fff', fontSize: 24 },
  row: { flexDirection: 'row', gap: Spacing.two, flexWrap: 'wrap' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two + 4 },
  achievement: { width: '47%', flexGrow: 1, gap: Spacing.one + 2 },
  medal: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center', marginBottom: 2 },
  locked: { opacity: 0.6 },
});
