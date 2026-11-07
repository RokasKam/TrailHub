import { useEffect, useState } from 'react';
import { Alert } from 'react-native';

import { Button } from '@/components/ui';
import { downloadOfflineRoute, offlineRouteProgress, useMapStyleUrl } from '@/components/trail-map';
import type { PlannedRoute } from '@/lib/types';

/**
 * "Save offline": stores map tiles along the route. The route itself (line, days, stats) is already kept by the
 * query cache once opened, so with this the route can be followed with no signal at all.
 */
export function SaveRouteOffline({ route }: { route: PlannedRoute }) {
  const styleUrl = useMapStyleUrl();
  const name = `route-${route.id}`;
  const [progress, setProgress] = useState<number | null>(null);

  useEffect(() => {
    let alive = true;
    void offlineRouteProgress(name).then((p) => alive && setProgress(p));
    return () => {
      alive = false;
    };
  }, [name]);

  const save = async () => {
    setProgress(0);
    try {
      await downloadOfflineRoute(name, route.path.map(([latitude, longitude]) => ({ latitude, longitude })), styleUrl, setProgress);
    } catch (e) {
      setProgress(null);
      Alert.alert("Couldn't save the map", (e as Error).message);
    }
  };

  const done = progress != null && progress >= 100;
  return (
    <Button
      icon={done ? 'checkmark.circle.fill' : 'arrow.down.circle'}
      variant="secondary"
      label={progress == null ? 'Save map offline' : done ? 'Map saved offline' : `Saving map ${progress}%`}
      disabled={progress != null}
      onPress={() => void save()}
    />
  );
}
