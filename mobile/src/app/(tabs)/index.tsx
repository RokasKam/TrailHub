import * as Location from 'expo-location';
import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Alert, ScrollView, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { type TrailFilters, useFavoriteTrails, useImportTrailGpx, useTrailSearch } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { TrailCard } from '@/components/trail-card';
import { TrailMap } from '@/components/trail-map';
import { Button, Card, Chip, Input, QueryState, Screen, Section } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { pickGpxFile, type PickedFile } from '@/lib/pick-gpx';
import type { Difficulty } from '@/lib/types';

const DIFFICULTIES: Difficulty[] = ['Easy', 'Moderate', 'Hard', 'Expert'];
const LENGTHS = [
  { label: '< 5 km', max: 5 },
  { label: '5–15 km', min: 5, max: 15 },
  { label: '15+ km', min: 15 },
] as const;
const DURATIONS = [
  { label: '< 2 h', max: 120 },
  { label: '< 4 h', max: 240 },
  { label: 'Full day', max: undefined },
] as const;

function useDebounced<T>(value: T, ms = 350) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return debounced;
}

export default function DiscoverScreen() {
  const units = useUnits();
  const [query, setQuery] = useState('');
  const [difficulty, setDifficulty] = useState<Difficulty[]>([]);
  const [length, setLength] = useState<number | null>(null);
  const [duration, setDuration] = useState<number | null>(null);
  const [near, setNear] = useState<{ lat: number; lon: number } | null>(null);
  const [view, setView] = useState<'list' | 'map'>('list');
  const debouncedQuery = useDebounced(query);

  const filters = useMemo<TrailFilters>(() => {
    const len = length != null ? LENGTHS[length] : undefined;
    return {
      q: debouncedQuery || undefined,
      difficulty: difficulty.length ? difficulty : undefined,
      minDistanceKm: len && 'min' in len ? len.min : undefined,
      maxDistanceKm: len && 'max' in len ? len.max : undefined,
      maxDurationMinutes: duration != null ? DURATIONS[duration].max : undefined,
      lat: near?.lat,
      lon: near?.lon,
      radiusKm: near ? 50 : undefined,
      sort: near ? 'Distance' : 'Relevance',
    };
  }, [debouncedQuery, difficulty, duration, length, near]);

  const search = useTrailSearch(filters);
  const favorites = useFavoriteTrails();

  const toggleNearMe = async () => {
    if (near) return setNear(null);
    const { status } = await Location.requestForegroundPermissionsAsync();
    if (status !== 'granted') return Alert.alert('Location needed', 'Allow location access to find trails near you.');
    try {
      const position = (await Location.getLastKnownPositionAsync()) ?? (await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }));
      setNear({ lat: position.coords.latitude, lon: position.coords.longitude });
    } catch {
      // No fix yet (indoors, airplane mode, or a simulator without a location set).
      Alert.alert('Location unavailable', "Couldn't get your current location. Try again outdoors, or search by region instead.");
    }
  };

  const trails = search.data?.items ?? [];

  return (
    <Screen tabBar>
      <View style={styles.titleRow}>
        <ThemedText type="subtitle">Discover</ThemedText>
        <ImportTrailButton />
      </View>

      <Input placeholder="Search trails or regions" value={query} onChangeText={setQuery} returnKeyType="search" clearButtonMode="while-editing" />

      {/* flexGrow 0: the page content flex-grows, and a horizontal ScrollView would otherwise take the spare height */}
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.chipScroller} contentContainerStyle={styles.chips}>
        <Chip label="📍 Near me" selected={!!near} onPress={toggleNearMe} />
        {DIFFICULTIES.map((d) => (
          <Chip key={d} label={d} selected={difficulty.includes(d)} onPress={() => setDifficulty((s) => (s.includes(d) ? s.filter((x) => x !== d) : [...s, d]))} />
        ))}
        {LENGTHS.map((l, i) => (
          <Chip key={l.label} label={l.label} selected={length === i} onPress={() => setLength(length === i ? null : i)} />
        ))}
        {DURATIONS.slice(0, 2).map((d, i) => (
          <Chip key={d.label} label={d.label} selected={duration === i} onPress={() => setDuration(duration === i ? null : i)} />
        ))}
      </ScrollView>

      <View style={styles.chips}>
        <Chip label="List" selected={view === 'list'} onPress={() => setView('list')} />
        <Chip label="Map" selected={view === 'map'} onPress={() => setView('map')} />
        <ThemedText type="small" themeColor="textSecondary" style={styles.count}>
          {search.data ? `${search.data.totalCount} trail${search.data.totalCount === 1 ? "" : "s"}` : ''}
        </ThemedText>
      </View>

      {view === 'map' ? (
        <TrailMap
          height={420}
          showUser={!!near}
          center={near ? { latitude: near.lat, longitude: near.lon } : trails[0]?.start}
          zoom={near ? 10 : 8}
          markers={trails.map((t) => ({ id: t.id, label: t.name, ...t.start }))}
          onMarkerPress={(id) => id && router.push(`/trail/${id}`)}
        />
      ) : (
        <>
          {!query && !near && !difficulty.length && (favorites.data?.length ?? 0) > 0 && (
            <Section title="Favorites">
              {favorites.data!.slice(0, 3).map((t) => (
                <TrailCard key={t.id} trail={t} units={units} />
              ))}
            </Section>
          )}
          <Section title={near ? 'Nearby' : 'Trails'}>
            <QueryState
              isLoading={search.isLoading}
              error={search.error}
              hasData={trails.length > 0}
              empty={!search.isLoading && trails.length === 0}
              emptyText="No trails match. Try fewer filters, or import a GPX file."
              onRetry={() => void Promise.all([search.refetch(), favorites.refetch()])}
            />
            {trails.map((t) => (
              <TrailCard key={t.id} trail={t} units={units} />
            ))}
          </Section>
        </>
      )}
    </Screen>
  );
}

function ImportTrailButton() {
  const importGpx = useImportTrailGpx();
  const [file, setFile] = useState<PickedFile | null>(null);
  const [region, setRegion] = useState('');
  const [difficulty, setDifficulty] = useState<Difficulty>('Moderate');

  const pick = async () => {
    const picked = await pickGpxFile();
    if (picked) setFile(picked);
  };

  // Takes the picked file as an argument (no `file!` here): React Compiler memoizes this
  // handler and reads its dependencies during render, before any file has been picked.
  const upload = (picked: PickedFile) =>
    importGpx.mutate(
      { uri: picked.uri, fileName: picked.name, region, difficulty },
      {
        onSuccess: ({ id }) => {
          setFile(null);
          router.push(`/trail/${id}`);
        },
        onError: (e) => Alert.alert('Import failed', e.message),
      },
    );

  if (!file) return <Button label="Import GPX" variant="secondary" onPress={pick} />;

  return (
    <Card style={styles.importCard}>
      <ThemedText type="smallBold">{file.name}</ThemedText>
      <Input placeholder="Region, e.g. High Tatras" value={region} onChangeText={setRegion} />
      <View style={styles.chips}>
        {DIFFICULTIES.map((d) => (
          <Chip key={d} label={d} selected={difficulty === d} onPress={() => setDifficulty(d)} />
        ))}
      </View>
      <View style={styles.chips}>
        <Button label="Cancel" variant="secondary" onPress={() => setFile(null)} />
        <Button label="Import" onPress={() => upload(file)} disabled={!region.trim()} loading={importGpx.isPending} />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  titleRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: Spacing.two },
  chips: { flexDirection: 'row', gap: Spacing.two, flexWrap: 'wrap', alignItems: 'center' },
  chipScroller: { flexGrow: 0 },
  count: { marginLeft: 'auto' },
  importCard: { width: '100%' },
});
