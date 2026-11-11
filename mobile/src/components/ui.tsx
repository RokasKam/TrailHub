import { GlassView, isLiquidGlassAvailable } from 'expo-glass-effect';
import { HeaderHeightContext } from 'expo-router/react-navigation';
import { SymbolView } from 'expo-symbols';
import materialWeight from 'expo-symbols/androidWeights/medium';
import { Children, Fragment, type PropsWithChildren, type ReactNode, use } from 'react';
import {
  ActivityIndicator,
  type ColorValue,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  TextInput,
  View,
  type StyleProp,
  type TextInputProps,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { BottomTabInset, Colors, MaxContentWidth, Radius, Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import { describeError } from '@/lib/api';
import { type AppIcon, ICONS } from '@/lib/icons';

/** Brand green for things drawn outside React styles (map layers, charts). Screens use `theme.accent`. */
export const Accent = Colors.light.accent;
export const Danger = Colors.light.danger;

const glass = isLiquidGlassAvailable();

/** SF Symbol on iOS, the matching Material Symbol on Android (see `ICONS`). */
export function Icon({ name, size = 17, color, weight = 'semibold', type = 'monochrome' }: { name: AppIcon; size?: number; color?: ColorValue; weight?: 'regular' | 'medium' | 'semibold' | 'bold'; type?: 'monochrome' | 'hierarchical' | 'multicolor' }) {
  const theme = useTheme();
  // A tint replaces the symbol's own palette, so multicolor symbols get none. Material Symbols have no
  // palette, so Android always takes the tint.
  const tint = type === 'multicolor' && Platform.OS === 'ios' ? undefined : (color ?? theme.text);
  return (
    <SymbolView
      name={{ ios: name, android: ICONS[name] }}
      size={size}
      tintColor={tint}
      // One Material weight for every icon: each weight is a separate font file to load.
      weight={{ ios: weight, android: materialWeight }}
      type={type}
      fallback={null}
      style={{ width: size, height: size }}
    />
  );
}

type ScreenProps = PropsWithChildren<{
  scroll?: boolean;
  tabBar?: boolean;
  /** Pull to refresh. */
  refreshing?: boolean;
  onRefresh?: () => void;
  /** Called when the user scrolls near the bottom, e.g. to load the next page. */
  onEndReached?: () => void;
}>;

/** Scrollable page with safe-area padding that clears the header and tab bar. */
export function Screen({ children, scroll = true, tabBar = false, refreshing = false, onRefresh, onEndReached }: ScreenProps) {
  const insets = useSafeAreaInsets();
  const theme = useTheme();
  // iOS 26+ stack headers are translucent and content scrolls under them; start below the header, not just the status bar.
  const headerHeight = use(HeaderHeightContext) ?? 0;
  const top = Math.max(insets.top, headerHeight);
  const padding = { paddingTop: top + Spacing.three, paddingBottom: insets.bottom + (tabBar ? BottomTabInset : 0) + Spacing.five };
  const content = <View style={[styles.content, !scroll && padding]}>{children}</View>;
  return scroll ? (
    <ScrollView
      style={{ flex: 1, backgroundColor: theme.background }}
      contentContainerStyle={[styles.scroll, padding]}
      keyboardShouldPersistTaps="handled"
      automaticallyAdjustKeyboardInsets // keeps the focused field above the keyboard (iOS)
      keyboardDismissMode="interactive"
      refreshControl={onRefresh ? <RefreshControl refreshing={refreshing} onRefresh={onRefresh} progressViewOffset={top} tintColor={theme.textSecondary} colors={[theme.accent]} /> : undefined}
      scrollEventThrottle={onEndReached ? 200 : undefined}
      onScroll={
        onEndReached
          ? ({ nativeEvent: { layoutMeasurement, contentOffset, contentSize } }) => {
              if (layoutMeasurement.height + contentOffset.y >= contentSize.height - 600) onEndReached();
            }
          : undefined
      }>
      {content}
    </ScrollView>
  ) : (
    <ThemedView style={{ flex: 1 }}>{content}</ThemedView>
  );
}

/** Large title for tab screens, with an optional line above it and a trailing action. */
export function PageHeader({ title, eyebrow, action }: { title: string; eyebrow?: string; action?: ReactNode }) {
  return (
    <View style={styles.pageHeader}>
      <View style={{ flex: 1 }}>
        {eyebrow && (
          <ThemedText type="caption" themeColor="textSecondary" style={styles.eyebrow}>
            {eyebrow.toUpperCase()}
          </ThemedText>
        )}
        <ThemedText type="largeTitle" numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.7}>
          {title}
        </ThemedText>
      </View>
      {action}
    </View>
  );
}

export function Card({ children, style, flat }: PropsWithChildren<{ style?: StyleProp<ViewStyle>; flat?: boolean }>) {
  const theme = useTheme();
  return <View style={[styles.card, { backgroundColor: theme.backgroundElement }, !flat && styles.raised, style]}>{children}</View>;
}

/** Floating material: Liquid Glass on iOS 26+, a translucent fill elsewhere. */
export function Glass({ children, style, interactive }: PropsWithChildren<{ style?: StyleProp<ViewStyle>; interactive?: boolean }>) {
  const theme = useTheme();
  if (glass) {
    return (
      <GlassView style={style} isInteractive={interactive}>
        {children}
      </GlassView>
    );
  }
  return <View style={[{ backgroundColor: theme.backgroundElement + 'E6' }, style]}>{children}</View>;
}

type ButtonProps = {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'tinted' | 'danger';
  icon?: AppIcon;
  disabled?: boolean;
  loading?: boolean;
  size?: 'regular' | 'large' | 'small';
};

export function Button({ label, onPress, variant = 'primary', icon, disabled, loading, size = 'regular' }: ButtonProps) {
  const theme = useTheme();
  const background = { primary: theme.accent, secondary: theme.backgroundSelected, tinted: theme.accentSoft, danger: theme.dangerSoft }[variant];
  const color = { primary: '#fff', secondary: theme.text, tinted: theme.accent, danger: theme.danger }[variant];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      disabled={disabled || loading}
      style={({ pressed }) => [
        styles.button,
        size === 'large' && styles.buttonLarge,
        size === 'small' && styles.buttonSmall,
        { backgroundColor: background, opacity: disabled ? 0.45 : 1, transform: [{ scale: pressed ? 0.97 : 1 }] },
      ]}>
      {loading ? <ActivityIndicator color={color} /> : icon && <Icon name={icon} color={color} size={size === 'small' ? 14 : 16} />}
      <ThemedText type="smallBold" style={[{ color }, size === 'large' && { fontSize: 17 }]} numberOfLines={1}>
        {label}
      </ThemedText>
    </Pressable>
  );
}

/** Round icon-only button, e.g. in a page header or floating over a map. */
export function IconButton({ icon, onPress, label, color, tinted }: { icon: AppIcon; onPress: () => void; label: string; color?: ColorValue; tinted?: boolean }) {
  const theme = useTheme();
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={label} onPress={onPress} hitSlop={6} style={({ pressed }) => ({ transform: [{ scale: pressed ? 0.92 : 1 }] })}>
      <View style={[styles.iconButton, { backgroundColor: tinted ? theme.accent : theme.backgroundElement }, styles.raised]}>
        <Icon name={icon} color={color ?? (tinted ? '#fff' : theme.text)} size={18} />
      </View>
    </Pressable>
  );
}

export function Chip({ label, selected, onPress, icon }: { label: string; selected?: boolean; onPress: () => void; icon?: AppIcon }) {
  const theme = useTheme();
  const color = selected ? theme.background : theme.text;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={({ pressed }) => [
        styles.chip,
        selected ? { backgroundColor: theme.text, borderColor: theme.text } : { backgroundColor: theme.backgroundElement, borderColor: theme.separator },
        pressed && { opacity: 0.7 },
      ]}>
      {icon && <Icon name={icon} size={13} color={color} />}
      <ThemedText type="small" style={{ color, fontWeight: 600 }}>
        {label}
      </ThemedText>
    </Pressable>
  );
}

/** iOS-style segmented control. */
export function Segmented<T extends string>({ options, value, onChange }: { options: { value: T; label: string; icon?: AppIcon }[]; value: T; onChange: (value: T) => void }) {
  const theme = useTheme();
  return (
    <View style={[styles.segmented, { backgroundColor: theme.backgroundSelected }]}>
      {options.map((o) => {
        const selected = o.value === value;
        return (
          <Pressable
            key={o.value}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            onPress={() => onChange(o.value)}
            style={[styles.segment, selected && [styles.raised, { backgroundColor: theme.backgroundElement }]]}>
            {o.icon && <Icon name={o.icon} size={13} color={selected ? theme.text : theme.textSecondary} />}
            <ThemedText type="small" style={{ fontWeight: 600, color: selected ? theme.text : theme.textSecondary }}>
              {o.label}
            </ThemedText>
          </Pressable>
        );
      })}
    </View>
  );
}

export function Badge({ label, color, icon, solid }: { label: string; color: string; icon?: AppIcon; solid?: boolean }) {
  return (
    <View style={[styles.badge, { backgroundColor: solid ? color : color + '22' }]}>
      {icon && <Icon name={icon} size={11} color={solid ? '#fff' : color} />}
      <ThemedText type="caption" style={{ color: solid ? '#fff' : color, fontWeight: 700 }}>
        {label}
      </ThemedText>
    </View>
  );
}

export function Stat({ label, value, icon }: { label: string; value: string; icon?: AppIcon }) {
  const theme = useTheme();
  return (
    <View style={styles.stat}>
      <ThemedText type="number" numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.6}>
        {value}
      </ThemedText>
      <View style={styles.statLabel}>
        {icon && <Icon name={icon} size={11} color={theme.textSecondary} />}
        <ThemedText type="caption" themeColor="textSecondary" numberOfLines={1}>
          {label}
        </ThemedText>
      </View>
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
        <ThemedText type="headline" style={{ flex: 1 }}>
          {title}
        </ThemedText>
        {action}
      </View>
      {children}
    </View>
  );
}

/** Grouped list (Settings-style): rows separated by hairlines inside one card. */
export function List({ children }: PropsWithChildren) {
  const theme = useTheme();
  const rows = Children.toArray(children).filter(Boolean);
  return (
    <Card style={styles.list}>
      {rows.map((row, i) => (
        <Fragment key={i}>
          {i > 0 && <View style={[styles.separator, { backgroundColor: theme.separator }]} />}
          {row}
        </Fragment>
      ))}
    </Card>
  );
}

export function ListRow({
  icon,
  iconColor,
  title,
  detail,
  trailing,
  onPress,
  destructive,
}: {
  icon?: AppIcon;
  iconColor?: string;
  title: string;
  detail?: string;
  trailing?: ReactNode;
  onPress?: () => void;
  destructive?: boolean;
}) {
  const theme = useTheme();
  const body = (
    <View style={styles.listRow}>
      {icon && (
        <View style={[styles.listIcon, { backgroundColor: iconColor ?? theme.accent }]}>
          <Icon name={icon} size={15} color="#fff" />
        </View>
      )}
      <View style={{ flex: 1 }}>
        <ThemedText style={destructive && { color: theme.danger }}>{title}</ThemedText>
        {detail && (
          <ThemedText type="small" themeColor="textSecondary">
            {detail}
          </ThemedText>
        )}
      </View>
      {trailing ?? (onPress && !destructive && <Icon name="chevron.right" size={13} color={theme.textSecondary} />)}
    </View>
  );
  return onPress ? (
    <Pressable onPress={onPress} accessibilityRole="button" style={({ pressed }) => pressed && { opacity: 0.6 }}>
      {body}
    </Pressable>
  ) : (
    body
  );
}

export function Input({ icon, ...props }: TextInputProps & { icon?: AppIcon }) {
  const theme = useTheme();
  return (
    <View style={[styles.inputWrap, { backgroundColor: theme.backgroundElement, borderColor: theme.separator }, props.multiline && { alignItems: 'flex-start' }]}>
      {icon && (
        // A multi-line box aligns to the top, so line the icon up with the first line of text (the input's top padding).
        <View style={props.multiline ? styles.multilineIcon : undefined}>
          <Icon name={icon} size={16} color={theme.textSecondary} />
        </View>
      )}
      <TextInput placeholderTextColor={theme.textSecondary} {...props} style={[styles.input, { color: theme.text }, props.style]} />
    </View>
  );
}

/** Centered icon + message for empty lists. */
export function EmptyState({ icon, title, message }: { icon: AppIcon; title: string; message?: string }) {
  const theme = useTheme();
  return (
    <View style={styles.empty}>
      <View style={[styles.emptyIcon, { backgroundColor: theme.accentSoft }]}>
        <Icon name={icon} size={26} color={theme.accent} />
      </View>
      <ThemedText type="smallBold" style={{ textAlign: 'center' }}>
        {title}
      </ThemedText>
      {message && (
        <ThemedText type="small" themeColor="textSecondary" style={{ textAlign: 'center' }}>
          {message}
        </ThemedText>
      )}
    </View>
  );
}

/** Loading / error / empty states, so screens handle all three the same way. */
export function QueryState({
  isLoading,
  error,
  hasData,
  empty,
  emptyText,
  emptyIcon = 'tray',
  onRetry,
}: {
  isLoading?: boolean;
  error?: Error | null;
  /** Cached results are on screen: a failed refresh becomes a quiet note instead of an error card. */
  hasData?: boolean;
  empty?: boolean;
  emptyText?: string;
  emptyIcon?: AppIcon;
  onRetry?: () => void;
}) {
  const theme = useTheme();
  if (isLoading) return <ActivityIndicator style={{ marginVertical: Spacing.four }} />;
  if (error && hasData)
    return (
      <Pressable onPress={onRetry} style={styles.offlineNote}>
        <Icon name="icloud.slash" size={13} color={theme.textSecondary} />
        <ThemedText type="caption" themeColor="textSecondary">
          Couldn&apos;t refresh, showing saved results.{onRetry ? ' Tap to retry.' : ''}
        </ThemedText>
      </Pressable>
    );
  if (error)
    return (
      <Card style={{ alignItems: 'center' }}>
        <EmptyState icon="wifi.exclamationmark" title={describeError(error)} />
        {onRetry && <Button label="Try again" variant="tinted" icon="arrow.clockwise" size="small" onPress={onRetry} />}
      </Card>
    );
  if (empty) return <EmptyState icon={emptyIcon} title={emptyText ?? 'Nothing here yet'} />;
  return null;
}

const styles = StyleSheet.create({
  scroll: { flexGrow: 1, alignItems: 'center' },
  content: { width: '100%', maxWidth: MaxContentWidth, paddingHorizontal: Spacing.three + 2, gap: Spacing.four, flexGrow: 1 },
  pageHeader: { flexDirection: 'row', alignItems: 'flex-end', gap: Spacing.three },
  eyebrow: { letterSpacing: 0.6, marginBottom: 2 },
  card: { borderRadius: Radius.large, borderCurve: 'continuous', padding: Spacing.three, gap: Spacing.two + 2 },
  raised: { boxShadow: '0 1px 2px rgba(0,0,0,0.05), 0 6px 20px rgba(0,0,0,0.05)' },
  button: {
    flexDirection: 'row',
    gap: Spacing.two,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: Spacing.four - 4,
    borderRadius: Radius.pill,
    minHeight: 48,
  },
  buttonLarge: { minHeight: 56 },
  buttonSmall: { minHeight: 36, paddingHorizontal: Spacing.three },
  iconButton: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  chip: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 14, height: 36, borderRadius: Radius.pill, borderWidth: StyleSheet.hairlineWidth },
  segmented: { flexDirection: 'row', borderRadius: Radius.pill, padding: 3 },
  segment: { flex: 1, flexDirection: 'row', gap: 6, alignItems: 'center', justifyContent: 'center', height: 32, borderRadius: Radius.pill },
  badge: { flexDirection: 'row', alignItems: 'center', gap: 4, borderRadius: Radius.pill, paddingHorizontal: 9, paddingVertical: 3, alignSelf: 'flex-start' },
  stat: { flex: 1, minWidth: 64, gap: 2 },
  statLabel: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  statRow: { flexDirection: 'row', flexWrap: 'wrap', rowGap: Spacing.three, columnGap: Spacing.three },
  section: { gap: Spacing.two + 4 },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  list: { paddingVertical: Spacing.one, gap: 0 },
  listRow: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three - 4, minHeight: 48, paddingVertical: Spacing.two },
  listIcon: { width: 30, height: 30, borderRadius: 8, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center' },
  separator: { height: StyleSheet.hairlineWidth, marginLeft: 42 },
  inputWrap: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two, borderRadius: Radius.medium, borderCurve: 'continuous', borderWidth: StyleSheet.hairlineWidth, paddingHorizontal: Spacing.three },
  input: { flex: 1, paddingVertical: 14, fontSize: 16 },
  multilineIcon: { marginTop: 16 },
  empty: { alignItems: 'center', gap: Spacing.two, paddingVertical: Spacing.four, paddingHorizontal: Spacing.four },
  emptyIcon: { width: 56, height: 56, borderRadius: 28, alignItems: 'center', justifyContent: 'center', marginBottom: Spacing.one },
  offlineNote: { flexDirection: 'row', alignItems: 'center', gap: 6 },
});
