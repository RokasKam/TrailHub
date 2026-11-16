import { NativeTabs } from 'expo-router/native-tabs';
import { unstable_getMaterialSymbolSourceAsync } from 'expo-symbols';
import { Platform, useColorScheme } from 'react-native';

import { Colors } from '@/constants/theme';
import { type AppIcon, ICONS } from '@/lib/icons';

// iOS uses SF Symbols; Android renders the matching Material Symbol to an image (the tab bar tints it).
const icon = (name: AppIcon) =>
  Platform.OS === 'ios' ? { sf: name } : { src: unstable_getMaterialSymbolSourceAsync(ICONS[name], 24, '#000') };

// iOS: the app pads screens for the status bar and tab bar itself (see Screen), so the system mustn't add its own
// insets to a tab's first scroll view too; that left an empty band above Discover.
const manualInsets = Platform.OS === 'ios';

export default function TabsLayout() {
  const scheme = useColorScheme();
  const colors = Colors[scheme === 'dark' ? 'dark' : 'light'];

  return (
    // No background on iOS: the system draws the Liquid Glass tab bar. Android keeps an opaque bar.
    <NativeTabs
      tintColor={colors.accent}
      backgroundColor={Platform.OS === 'android' ? colors.backgroundElement : undefined}
      indicatorColor={colors.accentSoft}
      labelVisibilityMode="labeled" // Android: label every tab, like iOS (the default shows only the selected one)
      labelStyle={{ selected: { color: colors.accent } }}>
      <NativeTabs.Trigger name="index" disableAutomaticContentInsets={manualInsets}>
        <NativeTabs.Trigger.Label>Discover</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('map.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="plan" disableAutomaticContentInsets={manualInsets}>
        <NativeTabs.Trigger.Label>Plan</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('point.topleft.down.to.point.bottomright.curvepath.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="record" disableAutomaticContentInsets={manualInsets}>
        <NativeTabs.Trigger.Label>Record</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('record.circle.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="activities" disableAutomaticContentInsets={manualInsets}>
        <NativeTabs.Trigger.Label>Hikes</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('figure.hiking')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="profile" disableAutomaticContentInsets={manualInsets}>
        <NativeTabs.Trigger.Label>Profile</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('person.crop.circle.fill')} />
      </NativeTabs.Trigger>
    </NativeTabs>
  );
}
