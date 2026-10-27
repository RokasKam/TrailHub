import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Switch, View } from 'react-native';

import { useAchievements, useProfile, useUpdatePreferences, useUpdateProfile } from '@/api/profile';
import { useSession } from '@/auth/session';
import { ThemedText } from '@/components/themed-text';
import { Accent, Button, Card, Chip, Input, QueryState, Screen, Section, Stat, StatRow } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { describeError } from '@/lib/api';
import { formatDistance, formatElevation } from '@/lib/format';
import type { MapStyle, Preferences, Units } from '@/lib/types';

export default function ProfileScreen() {
  const { signOut } = useSession();
  const profile = useProfile();
  const achievements = useAchievements();
  const updatePreferences = useUpdatePreferences();
  const updateProfile = useUpdateProfile();
  const [editing, setEditing] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [homeRegion, setHomeRegion] = useState('');

  if (!profile.data) {
    return (
      <Screen tabBar>
        <QueryState isLoading={profile.isLoading} error={profile.error} onRetry={() => profile.refetch()} />
        <Button label="Sign out" variant="secondary" onPress={signOut} />
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

  return (
    <Screen tabBar>
      {editing ? (
        <Card>
          <Input placeholder="Display name" value={displayName} onChangeText={setDisplayName} />
          <Input placeholder="Home region" value={homeRegion} onChangeText={setHomeRegion} />
          {updateProfile.error && <ThemedText type="small">{describeError(updateProfile.error)}</ThemedText>}
          <View style={styles.row}>
            <View style={{ flex: 1 }}>
              <Button label="Cancel" variant="secondary" onPress={() => setEditing(false)} />
            </View>
            <View style={{ flex: 1 }}>
              <Button
                label="Save"
                disabled={!displayName.trim()}
                loading={updateProfile.isPending}
                onPress={() => updateProfile.mutate({ displayName, homeRegion: homeRegion || null, bio: p.bio }, { onSuccess: () => setEditing(false) })}
              />
            </View>
          </View>
        </Card>
      ) : (
        <View style={styles.header}>
          <View style={{ flex: 1 }}>
            <ThemedText type="subtitle">{p.displayName}</ThemedText>
            <ThemedText type="small" themeColor="textSecondary">
              {[p.homeRegion, p.email].filter(Boolean).join(' · ') || 'Hiker'}
            </ThemedText>
          </View>
          <Button label="Edit" variant="secondary" onPress={startEditing} />
        </View>
      )}

      <Card>
        <StatRow>
          <Stat label="Hikes" value={String(p.totals.completedHikes)} />
          <Stat label="Distance" value={formatDistance(p.totals.distanceMeters, units)} />
          <Stat label="Climbed" value={formatElevation(p.totals.elevationGainMeters, units)} />
        </StatRow>
      </Card>

      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Button label="Trips" variant="secondary" onPress={() => router.push('/trips')} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Groups" variant="secondary" onPress={() => router.push('/groups')} />
        </View>
      </View>

      <Section title={`Achievements · ${p.achievementCount}/${achievements.data?.length ?? '–'}`}>
        <View style={styles.grid}>
          {achievements.data?.map((a) => (
            <Card key={a.code} style={[styles.achievement, !a.unlocked && styles.locked]}>
              <ThemedText type="smallBold">
                {a.unlocked ? '🏅' : '🔒'} {a.title}
              </ThemedText>
              <ThemedText type="small" themeColor="textSecondary">
                {a.description}
              </ThemedText>
            </Card>
          ))}
        </View>
      </Section>

      <Section title="Preferences">
        <Card>
          <ThemedText type="small">Units</ThemedText>
          <View style={styles.row}>
            {(['Metric', 'Imperial'] as Units[]).map((u) => (
              <Chip key={u} label={u} selected={units === u} onPress={() => setPreference({ units: u })} />
            ))}
          </View>
          <ThemedText type="small">Map style</ThemedText>
          <View style={styles.row}>
            {(['Outdoors', 'Terrain', 'Satellite'] as MapStyle[]).map((m) => (
              <Chip key={m} label={m} selected={p.preferences.mapStyle === m} onPress={() => setPreference({ mapStyle: m })} />
            ))}
          </View>
          <ToggleRow label="Share my location with groups" value={p.preferences.shareLocationWithGroups} onChange={(v) => setPreference({ shareLocationWithGroups: v })} />
          <ToggleRow label="Notifications" value={p.preferences.notificationsEnabled} onChange={(v) => setPreference({ notificationsEnabled: v })} />
        </Card>
      </Section>

      <Button label="Sign out" variant="secondary" onPress={signOut} />
    </Screen>
  );
}

function ToggleRow({ label, value, onChange }: { label: string; value: boolean; onChange: (value: boolean) => void }) {
  return (
    <View style={styles.toggle}>
      <ThemedText type="small" style={{ flex: 1 }}>
        {label}
      </ThemedText>
      <Switch value={value} onValueChange={onChange} trackColor={{ true: Accent }} />
    </View>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  row: { flexDirection: 'row', gap: Spacing.two, flexWrap: 'wrap' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two },
  achievement: { width: '48%', flexGrow: 1 },
  locked: { opacity: 0.55 },
  toggle: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two, paddingVertical: Spacing.one },
});
