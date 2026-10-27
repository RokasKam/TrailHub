import { Platform, StyleSheet, Text, type TextProps } from 'react-native';

import { Fonts, ThemeColor } from '@/constants/theme';
import { useTheme } from '@/hooks/use-theme';

export type ThemedTextProps = TextProps & {
  type?: 'default' | 'largeTitle' | 'title' | 'subtitle' | 'headline' | 'small' | 'smallBold' | 'caption' | 'number' | 'link' | 'linkPrimary' | 'code';
  themeColor?: ThemeColor;
};

export function ThemedText({ style, type = 'default', themeColor, ...rest }: ThemedTextProps) {
  const theme = useTheme();

  return <Text style={[{ color: theme[themeColor ?? 'text'] }, styles[type], style]} {...rest} />;
}

const styles = StyleSheet.create({
  /** Page titles on tab screens. */
  largeTitle: {
    fontSize: 34,
    lineHeight: 40,
    fontWeight: 800,
    letterSpacing: -0.8,
  },
  title: {
    fontSize: 44,
    lineHeight: 50,
    fontWeight: 800,
    letterSpacing: -1.2,
  },
  /** Screen and hero headings. */
  subtitle: {
    fontSize: 26,
    lineHeight: 32,
    fontWeight: 700,
    letterSpacing: -0.5,
  },
  /** Section headings and card titles. */
  headline: {
    fontSize: 19,
    lineHeight: 24,
    fontWeight: 700,
    letterSpacing: -0.3,
  },
  default: {
    fontSize: 16,
    lineHeight: 23,
    fontWeight: 400,
  },
  small: {
    fontSize: 14,
    lineHeight: 20,
    fontWeight: 400,
  },
  smallBold: {
    fontSize: 15,
    lineHeight: 20,
    fontWeight: 600,
  },
  caption: {
    fontSize: 12,
    lineHeight: 16,
    fontWeight: 500,
  },
  /** Stats: rounded numerals, like Fitness and Weather. */
  number: {
    fontFamily: Fonts.rounded,
    fontSize: 22,
    lineHeight: 28,
    fontWeight: 700,
    fontVariant: ['tabular-nums'],
  },
  link: {
    lineHeight: 30,
    fontSize: 14,
  },
  linkPrimary: {
    lineHeight: 30,
    fontSize: 14,
    color: '#3c87f7',
  },
  code: {
    fontFamily: Fonts.mono,
    fontWeight: Platform.select({ android: 700 }) ?? 500,
    fontSize: 12,
  },
});
