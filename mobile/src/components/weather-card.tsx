import { StyleSheet, View } from 'react-native';

import { useForecast } from '@/api/planning';
import { ThemedText } from '@/components/themed-text';
import { Card, QueryState } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import type { HikingSuitability, WeatherCondition } from '@/lib/types';

const ICONS: Record<WeatherCondition, string> = {
  Clear: '☀️',
  PartlyCloudy: '⛅️',
  Overcast: '☁️',
  Fog: '🌫️',
  Drizzle: '🌦️',
  Rain: '🌧️',
  FreezingRain: '🧊',
  Snow: '🌨️',
  Showers: '🌦️',
  Thunderstorm: '⛈️',
  Unknown: '·',
};

const SUITABILITY_COLORS: Record<HikingSuitability, string> = { Good: '#15803D', Caution: '#CA8A04', Poor: '#C2410C' };

/** Forecast at a trail (or trip) location, downscaled to the given elevation, with a per-day hiking verdict. */
export function WeatherCard({ latitude, longitude, elevation, from, to, days = 3 }: { latitude: number; longitude: number; elevation?: number | null; from?: string; to?: string; days?: number }) {
  const forecast = useForecast({ latitude, longitude, elevation }, { from, to });
  if (!forecast.data) return <QueryState isLoading={forecast.isLoading} error={forecast.error} />;

  const { current, currentCondition, daily } = forecast.data;
  return (
    <Card>
      {!from && (
        <ThemedText type="smallBold">
          {ICONS[currentCondition]} Now {Math.round(current.temperatureC)}°C · feels {Math.round(current.apparentTemperatureC)}°C · wind{' '}
          {Math.round(current.windSpeedKmh)} km/h
        </ThemedText>
      )}
      {daily.slice(0, days).map((d) => (
        <View key={d.forecast.date} style={styles.day}>
          <ThemedText type="small" style={styles.date}>
            {new Date(d.forecast.date).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })}
          </ThemedText>
          <ThemedText type="small" style={styles.icon}>
            {ICONS[d.condition]}
          </ThemedText>
          <ThemedText type="small" style={{ flex: 1 }}>
            {Math.round(d.forecast.minTemperatureC)}°/{Math.round(d.forecast.maxTemperatureC)}° · {d.forecast.precipitationProbabilityPercent}% rain
          </ThemedText>
          <View style={[styles.pill, { backgroundColor: SUITABILITY_COLORS[d.assessment.suitability] }]}>
            <ThemedText type="small" style={styles.pillText}>
              {d.assessment.suitability}
            </ThemedText>
          </View>
        </View>
      ))}
      {daily[0]?.assessment.warnings.slice(0, 2).map((w) => (
        <ThemedText key={w} type="small" themeColor="textSecondary">
          ⚠︎ {w}
        </ThemedText>
      ))}
    </Card>
  );
}

const styles = StyleSheet.create({
  day: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two },
  date: { width: 64 },
  icon: { width: 24 },
  pill: { borderRadius: Spacing.two, paddingHorizontal: Spacing.two },
  pillText: { color: '#fff', fontSize: 12 },
});
