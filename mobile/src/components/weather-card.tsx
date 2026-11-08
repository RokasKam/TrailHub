import type { AppIcon } from '@/lib/icons';
import { StyleSheet, View } from 'react-native';

import { useForecast } from '@/api/planning';
import { ThemedText } from '@/components/themed-text';
import { Badge, Card, Icon, QueryState } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';
import type { HikingSuitability, WeatherCondition } from '@/lib/types';

const ICONS: Record<WeatherCondition, AppIcon> = {
  Clear: 'sun.max.fill',
  PartlyCloudy: 'cloud.sun.fill',
  Overcast: 'cloud.fill',
  Fog: 'cloud.fog.fill',
  Drizzle: 'cloud.drizzle.fill',
  Rain: 'cloud.rain.fill',
  FreezingRain: 'cloud.sleet.fill',
  Snow: 'cloud.snow.fill',
  Showers: 'cloud.heavyrain.fill',
  Thunderstorm: 'cloud.bolt.rain.fill',
  Unknown: 'questionmark.circle',
};

const SUITABILITY_COLORS: Record<HikingSuitability, string> = { Good: '#16A34A', Caution: '#D97706', Poor: '#DC2626' };

/** Forecast at a trail (or trip) location, downscaled to the given elevation, with a per-day hiking verdict. */
export function WeatherCard({ latitude, longitude, elevation, from, to, days = 3 }: { latitude: number; longitude: number; elevation?: number | null; from?: string; to?: string; days?: number }) {
  const theme = useTheme();
  const forecast = useForecast({ latitude, longitude, elevation }, { from, to });
  if (!forecast.data) return <QueryState isLoading={forecast.isLoading} error={forecast.error} />;

  const { current, currentCondition, daily } = forecast.data;
  return (
    <Card>
      {!from && (
        <View style={styles.now}>
          <Icon name={ICONS[currentCondition]} size={40} type="multicolor" weight="regular" />
          <View style={{ flex: 1 }}>
            <ThemedText type="number" style={styles.temperature}>
              {Math.round(current.temperatureC)}°
            </ThemedText>
            <ThemedText type="small" themeColor="textSecondary">
              Feels {Math.round(current.apparentTemperatureC)}° · wind {Math.round(current.windSpeedKmh)} km/h
            </ThemedText>
          </View>
        </View>
      )}
      {daily.slice(0, days).map((d, i) => (
        <View key={d.forecast.date} style={[styles.day, { borderTopColor: theme.separator }, i === 0 && from != null && styles.firstDay]}>
          <ThemedText type="smallBold" style={styles.date}>
            {new Date(d.forecast.date).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' })}
          </ThemedText>
          <Icon name={ICONS[d.condition]} size={20} type="multicolor" weight="regular" />
          <ThemedText type="small" style={{ flex: 1 }}>
            {Math.round(d.forecast.minTemperatureC)}° / {Math.round(d.forecast.maxTemperatureC)}°
            <ThemedText type="small" themeColor="textSecondary">
              {'  '}
              {d.forecast.precipitationProbabilityPercent}% rain
            </ThemedText>
          </ThemedText>
          <Badge label={d.assessment.suitability} color={SUITABILITY_COLORS[d.assessment.suitability]} />
        </View>
      ))}
      {daily[0]?.assessment.warnings.slice(0, 2).map((w) => (
        <View key={w} style={styles.warning}>
          <Icon name="exclamationmark.triangle.fill" size={13} color="#D97706" />
          <ThemedText type="small" themeColor="textSecondary" style={{ flex: 1 }}>
            {w}
          </ThemedText>
        </View>
      ))}
    </Card>
  );
}

const styles = StyleSheet.create({
  now: { flexDirection: 'row', alignItems: 'center', gap: Spacing.three, paddingBottom: Spacing.one },
  temperature: { fontSize: 34, lineHeight: 40 },
  day: { flexDirection: 'row', alignItems: 'center', gap: Spacing.two + 2, paddingTop: Spacing.two + 2, borderTopWidth: StyleSheet.hairlineWidth },
  firstDay: { borderTopWidth: 0, paddingTop: 0 },
  date: { width: 64 },
  warning: { flexDirection: 'row', alignItems: 'flex-start', gap: Spacing.two },
});
