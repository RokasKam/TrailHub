import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import { Platform, Pressable, StyleSheet, View } from 'react-native';

import { ThemedText } from '@/components/themed-text';
import { Spacing } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';

type DateFieldProps = {
  label: string;
  value: Date;
  onChange: (value: Date) => void;
  mode?: 'date' | 'datetime';
  minimumDate?: Date;
};

/**
 * Native date (or date + time) input. iOS shows the inline compact picker; Android opens the system
 * dialog(s) when tapped (Android has no combined date-time dialog, so it asks for the date, then the time).
 */
export function DateField({ label, value, onChange, mode = 'date', minimumDate }: DateFieldProps) {
  const theme = useTheme();

  if (Platform.OS === 'ios') {
    return (
      <View style={styles.row}>
        <ThemedText type="small" style={styles.label}>
          {label}
        </ThemedText>
        <DateTimePicker
          value={value}
          mode={mode}
          display="compact"
          minimumDate={minimumDate}
          onValueChange={(_, date) => onChange(date)}
        />
      </View>
    );
  }

  const openAndroid = () =>
    DateTimePickerAndroid.open({
      value,
      mode: 'date',
      minimumDate,
      onValueChange: (_, date) => {
        if (mode === 'date') return onChange(date);
        DateTimePickerAndroid.open({
          value: date,
          mode: 'time',
          onValueChange: (_, time) => onChange(time),
        });
      },
    });

  return (
    <Pressable onPress={openAndroid} style={[styles.row, styles.androidField, { backgroundColor: theme.backgroundElement }]}>
      <ThemedText type="small" style={styles.label}>
        {label}
      </ThemedText>
      <ThemedText type="smallBold">
        {mode === 'date' ? value.toLocaleDateString() : value.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
      </ThemedText>
    </Pressable>
  );
}

/** yyyy-MM-dd in local time (the API's DateOnly format), not UTC. */
export function toDateOnly(date: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: Spacing.two },
  label: { flex: 1 },
  androidField: { borderRadius: Spacing.three, padding: Spacing.three },
});
