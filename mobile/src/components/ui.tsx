import { type PropsWithChildren, type ReactNode, use } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, TextInput, View, type StyleProp, type TextInputProps, type ViewStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { HeaderHeightContext } from 'expo-router/react-navigation';

import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { BottomTabInset, MaxContentWidth, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';

export const Accent = '#2F7D4F';
export const Danger = '#C2410C';

/** Scrollable page with safe-area padding that clears the tab bar. */
export function Screen({ children, scroll = true, tabBar = false }: PropsWithChildren<{ scroll?: boolean; tabBar?: boolean }>) {
  const insets = useSafeAreaInsets();
  const theme = useTheme();
  // iOS 26+ stack headers are translucent and content scrolls under them; start below the header, not just the status bar.
  const headerHeight = use(HeaderHeightContext) ?? 0;
  const padding = { paddingTop: Math.max(insets.top, headerHeight) + Spacing.three,paddingBottom: insets.bottom + (tabBar ? BottomTabInset : 0) + Spacing.four };
  const content = <View style={[styles.content, !scroll && padding]}>{children}</View>;
  return scroll ? (
    <ScrollView style={{ flex: 1, backgroundColor: theme.background }} contentContainerStyle={[styles.scroll, padding]}
      keyboardShouldPersistTaps="handled"
      automaticallyAdjustKeyboardInsets // keeps the focused field above the keyboard (iOS)
      keyboardDismissMode="interactive">
      {content}
    </ScrollView>
  ) : (
    <ThemedView style={{ flex: 1 }}>{content}</ThemedView>
  );
}

export function Card({ children, style }: PropsWithChildren<{ style?: StyleProp<ViewStyle> }>) {
  return (
    <ThemedView type="backgroundElement" style={[styles.card, style]}>
      {children}
    </ThemedView>
  );
}

type ButtonProps = { label: string; onPress: () => void; variant?: 'primary' | 'secondary' | 'danger'; disabled?: boolean; loading?: boolean; icon?: ReactNode };

export function Button({ label, onPress, variant = 'primary', disabled, loading, icon }: ButtonProps) {
  const theme = useTheme();
  const background = variant === 'primary' ? Accent : variant === 'danger' ? Danger : theme.backgroundSelected;
  const color = variant === 'secondary' ? theme.text : '#fff';
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={disabled || loading}
      style={({ pressed }) => [styles.button, { backgroundColor: background, opacity: disabled ? 0.5 : pressed ? 0.8 : 1 }]}>
      {loading ? <ActivityIndicator color={color} /> : icon}
      <ThemedText type="smallBold" style={{ color }}>
        {label}
      </ThemedText>
    </Pressable>
  );
}

export function Chip({ label, selected, onPress }: { label: string; selected?: boolean; onPress: () => void }) {
  const theme = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[styles.chip, { backgroundColor: selected ? Accent : theme.backgroundElement }]}>
      <ThemedText type="small" style={{ color: selected ? '#fff' : theme.text }}>
        {label}
      </ThemedText>
    </Pressable>
  );
}

export function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.stat}>
      <ThemedText type="smallBold" style={styles.statValue} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.7}>
        {value}
      </ThemedText>
      <ThemedText type="small" themeColor="textSecondary">
        {label}
      </ThemedText>
    </View>
  );
}

export function StatRow({ children }: PropsWithChildren) {
  return <View style={styles.statRow}>{children}</View>;
}

export function Section({ title, action, children }: PropsWithChildren<{ title: string; action?: ReactNode }>) {
  return (
    <View style={styles.section}>
      <View style={styles.sectionHeader}>
        <ThemedText type="smallBold" themeColor="textSecondary" style={styles.sectionTitle}>
          {title.toUpperCase()}
        </ThemedText>
        {action}
      </View>
      {children}
    </View>
  );
}

export function Input(props: TextInputProps) {
  const theme = useTheme();
  return (
    <TextInput
      placeholderTextColor={theme.textSecondary}
      {...props}
      style={[styles.input, { backgroundColor: theme.backgroundElement, color: theme.text }, props.style]}
    />
  );
}

/** Loading / error / empty states, so screens handle all three the same way. */
export function QueryState({
  isLoading,
  error,
  hasData,
  empty,
  emptyText,
  onRetry,
}: {
  isLoading?: boolean;
  error?: Error | null;
  /** Cached results are on screen: a failed refresh becomes a quiet note instead of an error card. */
  hasData?: boolean;
  empty?: boolean;
  emptyText?: string;
  onRetry?: () => void;
}) {
  if (isLoading) return <ActivityIndicator style={{ marginVertical: Spacing.four }} />;
  if (error && hasData)
    return (
      <ThemedText type="small" themeColor="textSecondary" onPress={onRetry}>
        Couldn&apos;t refresh, showing saved results.{onRetry ? ' Tap to retry.' : ''}
      </ThemedText>
    );
  if (error)
    return (
      <Card>
        <ThemedText type="small">{describeError(error)}</ThemedText>
        {onRetry && <Button label="Try again" variant="secondary" onPress={onRetry} />}
      </Card>
    );
  if (empty)
    return (
      <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center', marginVertical: Spacing.four }}>
        {emptyText}
      </ThemedText>
    );
  return null;
}

const styles = StyleSheet.create({
  scroll: { flexGrow: 1, alignItems: 'center' },
  content: { width: '100%', maxWidth: MaxContentWidth, paddingHorizontal: Spacing.three, gap: Spacing.three, flexGrow: 1 },
  card: { borderRadius: Spacing.three, padding: Spacing.three, gap: Spacing.two },
  button: { flexDirection: 'row', gap: Spacing.two, alignItems: 'center', justifyContent: 'center', paddingVertical: Spacing.three, paddingHorizontal: Spacing.four, borderRadius: Spacing.three, minHeight: 48 },
  chip: { paddingHorizontal: Spacing.three, paddingVertical: Spacing.two, borderRadius: Spacing.five },
  stat: { flex: 1, minWidth: 80, gap: Spacing.half },
  statValue: { fontSize: 18, lineHeight: 24 },
  statRow: { flexDirection: 'row', flexWrap: 'wrap', gap: Spacing.three },
  section: { gap: Spacing.two },
  sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  sectionTitle: { letterSpacing: 0.5 },
  input: { borderRadius: Spacing.three, paddingHorizontal: Spacing.three, paddingVertical: Spacing.three, fontSize: 16 },
});
