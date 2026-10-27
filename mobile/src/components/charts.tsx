import { useState } from 'react';
import { StyleSheet, View, type LayoutChangeEvent } from 'react-native';
import Svg, { Defs, LinearGradient, Path, Rect, Stop } from 'react-native-svg';

import { ThemedText } from '@/components/themed-text';
import { Accent } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { formatDistance, formatElevation } from '@/lib/format';
import type { ElevationSample, Units } from '@/lib/types';

function useWidth() {
  const [width, setWidth] = useState(0);
  return [width, (e: LayoutChangeEvent) => setWidth(e.nativeEvent.layout.width)] as const;
}

export function ElevationChart({ samples, units, height = 140 }: { samples: ElevationSample[]; units: Units; height?: number }) {
  const [width, onLayout] = useWidth();
  if (samples.length < 2) return null;

  const minE = Math.min(...samples.map((s) => s.elevationMeters));
  const maxE = Math.max(...samples.map((s) => s.elevationMeters));
  const total = samples[samples.length - 1].distanceMeters || 1;
  const range = Math.max(maxE - minE, 10);
  const x = (d: number) => (d / total) * width;
  const y = (e: number) => height - 8 - ((e - minE) / range) * (height - 16);

  const line = samples.map((s, i) => `${i === 0 ? 'M' : 'L'}${x(s.distanceMeters).toFixed(1)},${y(s.elevationMeters).toFixed(1)}`).join(' ');
  const area = `${line} L${width},${height} L0,${height} Z`;

  return (
    <View onLayout={onLayout} accessibilityLabel={`Elevation from ${formatElevation(minE, units)} to ${formatElevation(maxE, units)}`}>
      {width > 0 && (
        <Svg width={width} height={height}>
          <Defs>
            <LinearGradient id="fill" x1="0" y1="0" x2="0" y2="1">
              <Stop offset="0" stopColor={Accent} stopOpacity={0.45} />
              <Stop offset="1" stopColor={Accent} stopOpacity={0.05} />
            </LinearGradient>
          </Defs>
          <Path d={area} fill="url(#fill)" />
          <Path d={line} stroke={Accent} strokeWidth={2} fill="none" />
        </Svg>
      )}
      <View style={styles.axis}>
        <ThemedText type="small" themeColor="textSecondary">
          {formatElevation(minE, units)} – {formatElevation(maxE, units)}
        </ThemedText>
        <ThemedText type="small" themeColor="textSecondary">
          {formatDistance(total, units)}
        </ThemedText>
      </View>
    </View>
  );
}

export function BarChart({ values, labels, height = 120 }: { values: number[]; labels: string[]; height?: number }) {
  const [width, onLayout] = useWidth();
  const max = Math.max(...values, 1);
  const gap = 4;
  const barWidth = width > 0 ? (width - gap * (values.length - 1)) / values.length : 0;
  return (
    <View onLayout={onLayout}>
      {width > 0 && (
        <Svg width={width} height={height}>
          {values.map((v, i) => {
            const h = Math.max(v > 0 ? 3 : 1, (v / max) * (height - 4));
            return <Rect key={i} x={i * (barWidth + gap)} y={height - h} width={barWidth} height={h} rx={3} fill={Accent} opacity={v > 0 ? 1 : 0.25} />;
          })}
        </Svg>
      )}
      <View style={styles.axis}>
        <ThemedText type="small" themeColor="textSecondary">
          {labels[0]}
        </ThemedText>
        <ThemedText type="small" themeColor="textSecondary">
          {labels[labels.length - 1]}
        </ThemedText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  axis: { flexDirection: 'row', justifyContent: 'space-between', marginTop: Spacing.one },
});
