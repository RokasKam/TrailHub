import * as Location from 'expo-location';
import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Alert, ScrollView, StyleSheet, View } from 'react-native';

import { useUnits } from '@/api/profile';
import { type TrailFilters, useFavoriteTrails, useImportTrailGpx, useTrailSearch } from '@/api/trails';
import { ThemedText } from '@/components/themed-text';
import { TrailCard } from '@/components/trail-card';
import { TrailMap } from '@/components/trail-map';
import { Button, Card, Chip, IconButton, Input, PageHeader, QueryState, Screen, Section, Segmented } from '@/components/ui';
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

function greeting() {
  const hour = new Date().getHours();
  return hour < 5 ? 'Night owl hike?' : hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
}

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
  const [importing, setImporting] = useState<PickedFile | null>(null);
  const pickImport = async () => {
    const picked = await pickGpxFile();
    if (picked) setImporting(picked);
  };

  return (
    <Screen tabBar>
      <PageHeader eyebrow={greeting()} title="Discover" action={<IconButton icon="square.and.arrow.down" label="Import GPX" onPress={pickImport} />} />
      {importing && <ImportTrailCard file={importing} onDone={() => setImporting(null)} />}

      <Input icon="magnifyingglass" placeholder="Trails, peaks or regions" value={query} onChangeText={setQuery} returnKeyType="search" clearButtonMode="while-editing" />

      {/* flexGrow 0: the page content flex-grows, and a horizontal ScrollView would otherwise take the spare height */}
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.chipScroller} contentContainerStyle={[styles.chips, { paddingHorizontal: 18 }]}>
        <Chip label="Near me" icon="location.fill" selected={!!near} onPress={toggleNearMe} />
        {DIFFICULTIES.map((d) => (
          <Chip key={d} label={d} selected={difficulty.includes(d)} onPress={() => setDifficulty((s) => (s.includes(d) ? s.filter((x) => x !== d) : [...s, d]))} />
        ))}
        {LENGTHS.map((l, i) => (
          <Chip key={l.label} icon={i === 0 ? 'ruler' : undefined} label={l.label} selected={length === i} onPress={() => setLength(length === i ? null : i)} />
        ))}
        {DURATIONS.slice(0, 2).map((d, i) => (
          <Chip key={d.label} icon={i === 0 ? 'clock' : undefined} label={d.label} selected={duration === i} onPress={() => setDuration(duration === i ? null : i)} />
        ))}
      </ScrollView>

      <Segmented
        value={view}
        onChange={setView}
        options={[
          { value: 'list', label: 'List', icon: 'list.bullet' },
          { value: 'map', label: 'Map', icon: 'map' },
        ]}
      />

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
            <Section title="Your favorites">
              {/* Bleeds to the screen edges so cards scroll under the margin, like App Store shelves. */}
              <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.shelf} contentContainerStyle={styles.shelfContent}>
                {favorites.data!.map((t) => (
                  <TrailCard key={t.id} trail={t} units={units} compact />
                ))}
              </ScrollView>
            </Section>
          )}
          <Section
            title={near ? 'Nearby' : query ? 'Results' : 'All trails'}
            action={
              search.data && (
                <ThemedText type="small" themeColor="textSecondary">
                  {search.data.totalCount} trail{search.data.totalCount === 1 ? '' : 's'}
                </ThemedText>
              )
            }>
            <QueryState
              isLoading={search.isLoading}
              error={search.error}
              hasData={trails.length > 0}
              empty={!search.isLoading && trails.length === 0}
              emptyText="No trails match. Try fewer filters, or import a GPX file."
              emptyIcon="mountain.2"
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

function ImportTrailCard({ file, onDone }: { file: PickedFile; onDone: () => void }) {
  const importGpx = useImportTrailGpx();
  const [region, setRegion] = useState('');
  const [difficulty, setDifficulty] = useState<Difficulty>('Moderate');

  const upload = () =>
    importGpx.mutate(
      { uri: file.uri, fileName: file.name, region, difficulty },
      {
        onSuccess: ({ id }) => {
          onDone();
          router.push(`/trail/${id}`);
        },
        onError: (e) => Alert.alert('Import failed', e.message),
      },
    );

  return (
    <Card>
      <ThemedText type="headline">Import trail</ThemedText>
      <ThemedText type="small" themeColor="textSecondary" numberOfLines={1}>
        {file.name}
      </ThemedText>
      <Input icon="mappin.and.ellipse" placeholder="Region, e.g. High Tatras" value={region} onChangeText={setRegion} />
      <View style={styles.chips}>
        {DIFFICULTIES.map((d) => (
          <Chip key={d} label={d} selected={difficulty === d} onPress={() => setDifficulty(d)} />
        ))}
      </View>
      <View style={styles.chips}>
        <View style={{ flex: 1 }}>
          <Button label="Cancel" variant="secondary" onPress={onDone} />
        </View>
        <View style={{ flex: 1 }}>
          <Button label="Import" icon="square.and.arrow.down" onPress={upload} disabled={!region.trim()} loading={importGpx.isPending} />
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  chips: { flexDirection: 'row', gap: Spacing.two, flexWrap: 'wrap', alignItems: 'center' },
  // flexGrow 0 + negative margins: full-bleed horizontal rows inside the padded page.
  chipScroller: { flexGrow: 0, marginHorizontal: -18 },
  shelf: { flexGrow: 0, marginHorizontal: -18, overflow: 'visible' },
  shelfContent: { gap: Spacing.three, paddingHorizontal: 18, paddingBottom: Spacing.two },
});
