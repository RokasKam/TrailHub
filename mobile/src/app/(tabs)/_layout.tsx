import { NativeTabs } from 'expo-router/native-tabs';
import { unstable_getMaterialSymbolSourceAsync } from 'expo-symbols';
import { Platform, useColorScheme } from 'react-native';

import { Colors } from '@/constants/theme';
import { type AppIcon, ICONS } from '@/lib/icons';

// iOS uses SF Symbols; Android renders the matching Material Symbol to an image (the tab bar tints it).
const icon = (name: AppIcon) =>
  Platform.OS === 'ios' ? { sf: name } : { src: unstable_getMaterialSymbolSourceAsync(ICONS[name], 24, '#000') };

export default function TabsLayout() {
  const scheme = useColorScheme();
  const colors = Colors[scheme === 'dark' ? 'dark' : 'light'];

  return (
    // No background on iOS: the system draws the Liquid Glass tab bar. Android keeps an opaque bar.
    <NativeTabs
      tintColor={colors.accent}
      backgroundColor={Platform.OS === 'android' ? colors.backgroundElement : undefined}
      indicatorColor={colors.accentSoft}
      labelStyle={{ selected: { color: colors.accent } }}>
      <NativeTabs.Trigger name="index">
        <NativeTabs.Trigger.Label>Discover</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('map.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="plan">
        <NativeTabs.Trigger.Label>Plan</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('point.topleft.down.to.point.bottomright.curvepath.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="record">
        <NativeTabs.Trigger.Label>Record</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('record.circle.fill')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="activities">
        <NativeTabs.Trigger.Label>Hikes</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('figure.hiking')} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="profile">
        <NativeTabs.Trigger.Label>Profile</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('person.crop.circle.fill')} />
      </NativeTabs.Trigger>
    </NativeTabs>
  );
}
