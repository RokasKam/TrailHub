import { NativeTabs } from 'expo-router/native-tabs';
import { Platform, useColorScheme } from 'react-native';

import { Colors } from '@/constants/theme';

// iOS uses SF Symbols; Android falls back to the bundled template images.
const icon = (sf: string, android: number) =>
  Platform.OS === 'ios' ? { sf: sf as never } : { src: android, renderingMode: 'template' as const };

export default function TabsLayout() {
  const scheme = useColorScheme();
  const colors = Colors[scheme === 'dark' ? 'dark' : 'light'];

  return (
    <NativeTabs backgroundColor={colors.background} indicatorColor={colors.backgroundElement} labelStyle={{ selected: { color: colors.text } }}>
      <NativeTabs.Trigger name="index">
        <NativeTabs.Trigger.Label>Discover</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('map', require('@/assets/images/tabIcons/explore.png'))} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="record">
        <NativeTabs.Trigger.Label>Record</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('record.circle', require('@/assets/images/tabIcons/home.png'))} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="activities">
        <NativeTabs.Trigger.Label>Hikes</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('figure.hiking', require('@/assets/images/tabIcons/home.png'))} />
      </NativeTabs.Trigger>
      <NativeTabs.Trigger name="profile">
        <NativeTabs.Trigger.Label>Profile</NativeTabs.Trigger.Label>
        <NativeTabs.Trigger.Icon {...icon('person.crop.circle', require('@/assets/images/tabIcons/home.png'))} />
      </NativeTabs.Trigger>
    </NativeTabs>
  );
}
