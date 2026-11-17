import { usePathname } from 'expo-router';
import { useState } from 'react';
import { Alert, KeyboardAvoidingView, Modal, Platform, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useProfile, useUpdateProfile } from '@/api/profile';
import { ThemedText } from '@/components/themed-text';
import { Button, Icon, Input } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';
import { useRecordingStore } from '@/stores/recording-store';

const MAX_NAME = 80;

/**
 * Asks once what to call the person while their profile name is still the one from sign-up (often made from their
 * email). Rendered next to the root stack while signed in; shown at most once per app start, and never over a
 * hike being recorded.
 */
export function NamePrompt() {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const profile = useProfile();
  const update = useUpdateProfile();
  const pathname = usePathname();
  const recording = useRecordingStore((s) => s.status !== 'idle');
  const [closed, setClosed] = useState(false);
  const [draft, setDraft] = useState<string | null>(null);
  const [saving, setSaving] = useState<'save' | 'keep' | null>(null);

  const p = profile.data;
  // Fresh from the server, not the offline cache, which may predate a name confirmed since.
  const visible = !closed && profile.isFetchedAfterMount && p?.nameConfirmed === false && !(pathname === '/record' && recording);
  const current = p?.displayName ?? '';
  const name = draft ?? current;

  const close = () => {
    if (saving) return;
    setClosed(true);
  };
  const save = (displayName: string, which: 'save' | 'keep') => {
    if (!p || saving || !displayName.trim()) return;
    setSaving(which);
    update.mutate(
      { displayName: displayName.trim(), bio: p.bio, homeRegion: p.homeRegion, avatarUrl: p.avatarUrl },
      {
        onSuccess: () => setClosed(true),
        onError: (e) => Alert.alert('Couldn’t save your name', describeError(e)),
        onSettled: () => setSaving(null),
      },
    );
  };

  // iOS shows a page sheet below the status bar; Android's modal covers the whole screen.
  const top = Platform.OS === 'ios' ? Spacing.three : insets.top + Spacing.two;
  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={close}>
      <KeyboardAvoidingView style={{ flex: 1, backgroundColor: theme.background }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <View style={[styles.header, { paddingTop: top, borderBottomColor: theme.separator }]}>
          <ThemedText type="smallBold" numberOfLines={1} style={styles.headerTitle}>
            Your name
          </ThemedText>
        </View>
        <ScrollView contentContainerStyle={[styles.body, { paddingBottom: insets.bottom + Spacing.four }]} keyboardShouldPersistTaps="handled">
          <View style={styles.intro}>
            <View style={[styles.introIcon, { backgroundColor: theme.accentSoft }]}>
              <Icon name="person.fill" size={20} color={theme.accent} />
            </View>
            <ThemedText type="headline" style={{ textAlign: 'center' }}>
              What should we call you?
            </ThemedText>
            <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
              Your groups see this name on group hikes, trips, expenses and shared hikes. You can change it later in your profile.
            </ThemedText>
          </View>

          <Input
            icon="person"
            value={name}
            onChangeText={setDraft}
            placeholder="Your name"
            autoCapitalize="words"
            autoCorrect={false}
            textContentType="name"
            maxLength={MAX_NAME}
            returnKeyType="done"
            onSubmitEditing={() => save(name, 'save')}
          />

          <Button label="Save" icon="checkmark" size="large" disabled={!name.trim() || saving === 'keep'} loading={saving === 'save'} onPress={() => save(name, 'save')} />
          <Button label="Keep this name" variant="secondary" disabled={!current || saving === 'save'} loading={saving === 'keep'} onPress={() => save(current, 'keep')} />
        </ScrollView>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: Spacing.three, paddingBottom: Spacing.two + 2, borderBottomWidth: StyleSheet.hairlineWidth },
  headerTitle: { flex: 1, textAlign: 'center' },
  body: { padding: Spacing.three + 2, gap: Spacing.three },
  intro: { alignItems: 'center', gap: Spacing.one + 2, paddingVertical: Spacing.two },
  introIcon: { width: 48, height: 48, borderRadius: 24, alignItems: 'center', justifyContent: 'center', marginBottom: Spacing.one },
});
