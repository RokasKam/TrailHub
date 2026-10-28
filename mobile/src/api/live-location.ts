import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { groupKeys, tripKeys } from '@/api/planning';
import { config } from '@/config';
import { currentAuthHeader } from '@/lib/api';
import type { LiveLocation } from '@/lib/types';

/** Token for the WebSocket query string: the header value without its scheme. */
async function hubToken() {
  return ((await currentAuthHeader()) ?? '').replace(/^(Bearer|Dev) /, '');
}

/**
 * Joins a group's live-location channel over SignalR to receive other members' positions. They're
 * merged into the same query cache the REST endpoint fills, so the map only reads one source.
 * Sending your own position goes over REST (see location/group-sharing.ts) so it works in the background.
 */
export function useGroupLiveLocation(groupId: string) {
  const qc = useQueryClient();
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected);

  useEffect(() => {
    const hub = new HubConnectionBuilder()
      .withUrl(`${config.apiUrl}/hubs/groups`, { accessTokenFactory: hubToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const upsert = (location: LiveLocation) =>
      qc.setQueryData<LiveLocation[]>(groupKeys.locations(groupId), (all = []) => [...all.filter((l) => l.userId !== location.userId), location]);

    // Handlers must return nothing: SignalR treats a return value as a client result the server didn't ask for.
    hub.on('LocationUpdated', (_groupId: string, location: LiveLocation) => {
      upsert(location);
    });
    hub.on('LocationRemoved', (_groupId: string, userId: string) => {
      qc.setQueryData<LiveLocation[]>(groupKeys.locations(groupId), (all = []) => all.filter((l) => l.userId !== userId));
    });
    hub.onreconnecting(() => setState(hub.state));
    hub.onreconnected(async () => {
      setState(hub.state);
      qc.setQueryData(groupKeys.locations(groupId), await hub.invoke<LiveLocation[]>('JoinGroup', groupId));
    });
    hub.onclose(() => setState(hub.state));

    let cancelled = false;
    hub
      .start()
      .then(async () => {
        if (cancelled) return;
        setState(hub.state);
        qc.setQueryData(groupKeys.locations(groupId), await hub.invoke<LiveLocation[]>('JoinGroup', groupId));
      })
      .catch(() => setState(hub.state));

    return () => {
      cancelled = true;
      void hub.stop();
    };
  }, [groupId, qc]);

  return { state };
}

/**
 * Watches an open trip over SignalR (/hubs/trips). The server only says "this trip changed"; the trip is
 * then reloaded over REST, so what's shown always comes from the same endpoint and permission checks.
 */
export function useTripLiveUpdates(tripId: string, enabled: boolean) {
  const qc = useQueryClient();

  useEffect(() => {
    if (!enabled) return;
    const hub = new HubConnectionBuilder()
      .withUrl(`${config.apiUrl}/hubs/trips`, { accessTokenFactory: hubToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const reload = () => qc.invalidateQueries({ queryKey: tripKeys.detail(tripId) });
    hub.on('TripChanged', () => {
      void reload();
    });
    hub.onreconnected(async () => {
      await hub.invoke('WatchTrip', tripId);
      void reload(); // catch up on anything missed while offline
    });

    let cancelled = false;
    hub
      .start()
      .then(() => (cancelled ? undefined : hub.invoke('WatchTrip', tripId)))
      .catch(() => undefined);

    return () => {
      cancelled = true;
      void hub.stop();
    };
  }, [tripId, enabled, qc]);
}
