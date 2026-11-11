import { useState } from 'react';
import { Alert, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ThemedText } from '@/components/themed-text';
import { Button, Chip, Icon, Input } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';
import type { ContentReportReason } from '@/lib/types';

export type ReasonOption<R extends string> = { value: R; label: string; hint: string };

export const CONTENT_REPORT_REASONS: ReasonOption<ContentReportReason>[] = [
  { value: 'Inappropriate', label: 'Inappropriate', hint: 'Offensive, explicit or unrelated to hiking.' },
  { value: 'Spam', label: 'Spam', hint: 'Advertising, scams or repeated posts.' },
  { value: 'Harassment', label: 'Harassment', hint: 'Targets, insults or threatens someone.' },
  { value: 'Other', label: 'Other', hint: 'Something else. Tell us below.' },
];

const MAX_DETAILS = 1000;

type Props<R extends string> = {
  visible: boolean;
  onClose: () => void;
  /** e.g. "Report photo". */
  title: string;
  subtitle?: string;
  reasons: ReasonOption<R>[];
  /** Sends the report; the sheet shows progress, errors and the thank-you. */
  onSubmit: (report: { reason: R; details: string | null }) => Promise<unknown>;
  /** Extra line under the thank-you. */
  thanksMessage?: string;
};

/**
 * Reason chips, optional details and Send. Used for trails, trail photos, hike photos and comments.
 * Opened from inside another modal (a photo viewer), render it inside that modal's tree so iOS can present it.
 */
export function ReportSheet<R extends string>({ visible, onClose, title, subtitle, reasons, onSubmit, thanksMessage }: Props<R>) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const [reason, setReason] = useState<R | null>(null);
  const [details, setDetails] = useState('');
  const [sending, setSending] = useState(false);
  const selected = reasons.find((r) => r.value === reason);

  const reset = () => {
    setReason(null);
    setDetails('');
  };
  const close = () => {
    if (sending) return;
    reset();
    onClose();
  };
  const send = async () => {
    if (!reason || sending) return;
    setSending(true);
    try {
      await onSubmit({ reason, details: details.trim() || null });
      // Shown over the sheet, which closes on OK: an alert raised while a sheet is dismissing can get lost on iOS.
      Alert.alert('Thanks — a moderator will take a look', thanksMessage, [
        {
          text: 'OK',
          onPress: () => {
            reset();
            onClose();
          },
        },
      ]);
    } catch (e) {
      Alert.alert('Couldn’t send the report', describeError(e as Error));
    } finally {
      setSending(false);
    }
  };

  // iOS shows a page sheet below the status bar; Android's modal covers the whole screen.
  const top = Platform.OS === 'ios' ? Spacing.three : insets.top + Spacing.two;
  return (
    <Modal visible={visible} animationType="slide" presentationStyle="pageSheet" onRequestClose={close}>
      <KeyboardAvoidingView style={{ flex: 1, backgroundColor: theme.background }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <View style={[styles.header, { paddingTop: top, borderBottomColor: theme.separator }]}>
          <Pressable accessibilityRole="button" accessibilityLabel="Cancel" hitSlop={10} onPress={close} disabled={sending} style={styles.headerSide}>
            <ThemedText style={{ color: theme.accent }}>Cancel</ThemedText>
          </Pressable>
          <ThemedText type="smallBold" numberOfLines={1} style={styles.headerTitle}>
            {title}
          </ThemedText>
          <View style={styles.headerSide} />
        </View>
        <ScrollView contentContainerStyle={[styles.body, { paddingBottom: insets.bottom + Spacing.four }]} keyboardShouldPersistTaps="handled">
          <View style={styles.intro}>
            <View style={[styles.introIcon, { backgroundColor: theme.dangerSoft }]}>
              <Icon name="flag.fill" size={20} color={theme.danger} />
            </View>
            <ThemedText type="headline" style={{ textAlign: 'center' }}>
              What’s wrong with it?
            </ThemedText>
            <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
              {subtitle ?? 'Reports are private. Items reported by several people are hidden until a moderator reviews them.'}
            </ThemedText>
          </View>

          <View style={styles.chips}>
            {reasons.map((r) => (
              <Chip key={r.value} label={r.label} selected={r.value === reason} onPress={() => setReason(r.value)} />
            ))}
          </View>
          <ThemedText type="caption" themeColor="textSecondary" style={styles.hint}>
            {selected?.hint ?? 'Pick a reason.'}
          </ThemedText>

          <Input
            icon="textformat"
            value={details}
            onChangeText={setDetails}
            placeholder={reason === 'Other' ? 'What’s the problem? (recommended)' : 'Anything else? (optional)'}
            multiline
            maxLength={MAX_DETAILS}
            style={styles.details}
          />
          {details.length > MAX_DETAILS - 100 && (
            <ThemedText type="caption" themeColor="textSecondary" style={{ textAlign: 'right' }}>
              {details.length}/{MAX_DETAILS}
            </ThemedText>
          )}

          <Button label="Send report" icon="paperplane.fill" size="large" disabled={!reason} loading={sending} onPress={send} />
        </ScrollView>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  header: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: Spacing.three, paddingBottom: Spacing.two + 2, borderBottomWidth: StyleSheet.hairlineWidth },
  headerSide: { width: 72 },
  headerTitle: { flex: 1, textAlign: 'center' },
  body: { padding: Spacing.three + 2, gap: Spacing.three },
  intro: { alignItems: 'center', gap: Spacing.one + 2, paddingVertical: Spacing.two },
  introIcon: { width: 48, height: 48, borderRadius: 24, alignItems: 'center', justifyContent: 'center', marginBottom: Spacing.one },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.two, justifyContent: 'center' },
  hint: { textAlign: 'center', marginTop: -Spacing.one },
  details: { minHeight: 96, textAlignVertical: 'top' },
});
