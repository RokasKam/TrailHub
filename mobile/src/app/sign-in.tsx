import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { useSession } from '@/auth/session';
import { ThemedText } from '@/components/themed-text';
import { Button, Input, Screen } from '@/components/ui';
import { config } from '@/config';
import { Spacing } from '@/constants/theme';

export default function SignInScreen() {
  const { signIn } = useSession();
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
        <ThemedText type="title">TrailHub</ThemedText>
        <ThemedText themeColor="textSecondary" style={styles.center}>
          Discover trails, record hikes, plan trips{'\n'}and hike together.
        </ThemedText>
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
            <Button label="Log in" onPress={() => submit()} loading={busy} />
            <Button label="Create account" variant="secondary" onPress={() => submit(true)} disabled={busy} />
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

const styles = StyleSheet.create({
  hero: { flex: 1, justifyContent: 'center', alignItems: 'center', gap: Spacing.three },
  center: { textAlign: 'center' },
  actions: { gap: Spacing.three, paddingBottom: Spacing.five },
  error: { color: '#C2410C', textAlign: 'center' },
});
