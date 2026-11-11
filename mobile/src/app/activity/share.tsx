import { router, Stack, useLocalSearchParams } from 'expo-router';
import { Platform, Pressable } from 'react-native';

import { ShareHike } from '@/components/share-hike';
import { Icon, Screen } from '@/components/ui';
import { useTheme } from '@/hooks/use-theme';

/** Modal share sheet for a hike: /activity/share?id=… */
export default function ShareHikeScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const theme = useTheme();
  return (
    <Screen>
      <Stack.Screen
        options={{
          // iOS modals have no back button; Android's header keeps its back arrow.
          headerRight: Platform.OS !== 'ios' ? undefined : () => (
            <Pressable accessibilityRole="button" accessibilityLabel="Close" hitSlop={12} onPress={() => router.back()}>
              <Icon name="xmark" size={18} color={theme.text} />
            </Pressable>
          ),
        }}
      />
      <ShareHike activityId={id} />
    </Screen>
  );
}
