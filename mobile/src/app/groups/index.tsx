import { router } from 'expo-router';
import { useState } from 'react';

import { useCreateGroup, useGroups, useJoinGroup } from '@/api/planning';
import { useProfile } from '@/api/profile';
import { ThemedText } from '@/components/themed-text';
import { Button, Card, Input, List, ListRow, QueryState, Screen, Section } from '@/components/ui';
import { formatDate } from '@/lib/format';

export default function GroupsScreen() {
  const groups = useGroups();
  const displayName = useProfile().data?.displayName;
  const create = useCreateGroup();
  const join = useJoinGroup();
  const [name, setName] = useState('');
  const [code, setCode] = useState('');

  return (
    <Screen>
      <Section title="Join with an invite code">
        <Card>
          <Input icon="ticket" placeholder="8-character code" value={code} onChangeText={setCode} autoCapitalize="characters" maxLength={8} />
          {join.error && <ThemedText type="small">{join.error.message}</ThemedText>}
          <Button
            label="Join group"
            disabled={code.trim().length !== 8}
            loading={join.isPending}
            onPress={() => join.mutate({ inviteCode: code, displayName }, { onSuccess: (g) => router.push(`/groups/${g.id}`) })}
          />
        </Card>
      </Section>

      <Section title="Start a group">
        <Card>
          <Input icon="person.3" placeholder="Group name" value={name} onChangeText={setName} />
          <Button
            label="Create group"
            disabled={!name.trim()}
            loading={create.isPending}
            onPress={() => create.mutate({ name, displayName }, { onSuccess: (g) => router.push(`/groups/${g.id}`) })}
          />
        </Card>
      </Section>

      <Section title="Your groups">
        <QueryState isLoading={groups.isLoading} error={groups.error} hasData={!!groups.data?.length} empty={groups.data?.length === 0} emptyText="You're not in any groups yet." emptyIcon="person.3" />
        {!!groups.data?.length && (
          <List>
            {groups.data.map((g) => (
              <ListRow
                key={g.id}
                icon="person.3.fill"
                iconColor="#2563EB"
                title={g.name}
                detail={`${g.memberCount} member${g.memberCount > 1 ? 's' : ''} · ${g.myRole}${g.nextHikeAtUtc ? ` · next hike ${formatDate(g.nextHikeAtUtc)}` : ''}`}
                onPress={() => router.push(`/groups/${g.id}`)}
              />
            ))}
          </List>
        )}
      </Section>
    </Screen>
  );
}
