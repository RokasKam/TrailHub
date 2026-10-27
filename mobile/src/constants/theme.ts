/**
 * Below are the colors that are used in the app. The colors are defined in the light and dark mode.
 * There are many other ways to style your app. For example, [Nativewind](https://www.nativewind.dev/), [Tamagui](https://tamagui.dev/), [unistyles](https://unistyl.es/), etc.
 */

import '@/global.css';

import { Platform } from 'react-native';

// Warm "trail" neutrals: pages are a soft stone grey and cards sit on them as raised surfaces.
export const Colors = {
  light: {
    text: '#15181A',
    background: '#F3F2EE',
    backgroundElement: '#FFFFFF',
    backgroundSelected: '#E7E6E0',
    textSecondary: '#6E716B',
    separator: 'rgba(21,24,26,0.08)',
    accent: '#1F7A4D',
    accentSoft: '#E1F0E6',
    danger: '#C2410C',
    dangerSoft: '#FBE7DD',
  },
  dark: {
    text: '#F3F3F0',
    background: '#0C0E0D',
    backgroundElement: '#1A1D1B',
    backgroundSelected: '#272B28',
    textSecondary: '#9DA29B',
    separator: 'rgba(255,255,255,0.08)',
    accent: '#41C07E',
    accentSoft: '#173726',
    danger: '#F2734A',
    dangerSoft: '#3A1D12',
  },
} as const;

export const Radius = { small: 10, medium: 16, large: 24, pill: 999 } as const;

export type ThemeColor = keyof typeof Colors.light & keyof typeof Colors.dark;

export const Fonts = Platform.select({
  ios: {
    /** iOS `UIFontDescriptorSystemDesignDefault` */
    sans: 'system-ui',
    /** iOS `UIFontDescriptorSystemDesignSerif` */
    serif: 'ui-serif',
    /** iOS `UIFontDescriptorSystemDesignRounded` */
    rounded: 'ui-rounded',
    /** iOS `UIFontDescriptorSystemDesignMonospaced` */
    mono: 'ui-monospace',
  },
  default: {
    sans: 'normal',
    serif: 'serif',
    rounded: 'normal',
    mono: 'monospace',
  },
  web: {
    sans: 'var(--font-display)',
    serif: 'var(--font-serif)',
    rounded: 'var(--font-rounded)',
    mono: 'var(--font-mono)',
  },
});

export const Spacing = {
  half: 2,
  one: 4,
  two: 8,
  three: 16,
  four: 24,
  five: 32,
  six: 64,
} as const;

export const BottomTabInset = Platform.select({ ios: 50, android: 80 }) ?? 0;
export const MaxContentWidth = 800;
