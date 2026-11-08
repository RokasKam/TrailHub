import type { AppIcon } from '@/lib/icons';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useSession } from '@/auth/session';
import { ThemedText } from '@/components/themed-text';
import { Button, Icon, Input, Screen } from '@/components/ui';
import { config } from '@/config';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';

export default function SignInScreen() {
  const { signIn } = useSession();
  const theme = useTheme();
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (signUp = false) => {
    setBusy(true);
    setError(null);
    try {
      await signIn({ signUp, name: name || undefined });
    } catch (e) {
      // User closing the Auth0 browser sheet lands here too; that's not worth an alarming message.
      setError((e as Error).message?.includes('cancel') ? null : (e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen scroll={false}>
      <View style={styles.hero}>
        <View style={[styles.logo, { backgroundColor: theme.accent }]}>
          <Icon name="mountain.2.fill" size={44} color="#fff" />
        </View>
        <ThemedText type="title">TrailHub</ThemedText>
        <ThemedText themeColor="textSecondary" style={styles.center}>
          Discover trails, record hikes, plan trips{'\n'}and hike together.
        </ThemedText>
        <View style={styles.features}>
          <Feature icon="map.fill" color="#16A34A" text="Trails with maps, weather and conditions" />
          <Feature icon="location.north.line.fill" color="#2563EB" text="GPS recording that works offline" />
          <Feature icon="person.3.fill" color="#EA580C" text="Group hikes with live location" />
        </View>
      </View>
      <View style={styles.actions}>
        {config.authMode === 'development' ? (
          <>
            <ThemedText type="small" themeColor="textSecondary">
              Development sign-in: any name becomes your user id. Requires the backend in Auth:Mode=Development.
            </ThemedText>
            <Input placeholder="Your name" value={name} onChangeText={setName} autoCapitalize="none" autoCorrect={false} onSubmitEditing={() => submit()} />
            <Button label="Continue" onPress={() => submit()} loading={busy} />
          </>
        ) : (
          <>
            <Button size="large" label="Log in" onPress={() => submit()} loading={busy} />
            <Button size="large" label="Create account" variant="secondary" onPress={() => submit(true)} disabled={busy} />
            <ThemedText type="small" themeColor="textSecondary" style={styles.center}>
              Secured by Auth0. Sign up with email or a social account.
            </ThemedText>
          </>
        )}
        {error && (
          <ThemedText type="small" style={styles.error}>
            {error}
          </ThemedText>
        )}
      </View>
    </Screen>
  );
}

function Feature({ icon, color, text }: { icon: AppIcon; color: string; text: string }) {
  return (
    <View style={styles.feature}>
      <View style={[styles.featureIcon, { backgroundColor: color + '1F' }]}>
        <Icon name={icon} size={17} color={color} />
      </View>
      <ThemedText type="small" style={{ flex: 1 }}>
        {text}
      </ThemedText>
    </View>
  );
}

const styles = StyleSheet.create({
  hero: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: Spacing.three },
  logo: { width: 88, height: 88, borderRadius: 24, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center', marginBottom: Spacing.two },
  features: { alignSelf: 'stretch', gap: Spacing.three, marginTop: Spacing.four },
  feature: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three },
  featureIcon: { width: 36, height: 36, borderRadius: 10, borderCurve: 'continuous', alignItems: 'center', justifyContent: 'center' },
  center: { textAlign: 'center' },
  actions: { gap: Spacing.three, paddingBottom: Spacing.five },
  error: { color: '#C2410C', textAlign: 'center' },
});
