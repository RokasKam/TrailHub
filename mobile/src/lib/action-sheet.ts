import { ActionSheetIOS, Alert, Platform } from 'react-native';

export type SheetAction = { label: string; onPress: () => void; destructive?: boolean };

/** Native action sheet on iOS, an alert with buttons on Android. Does nothing when there are no actions. */
export function showActions(title: string | undefined, actions: SheetAction[]) {
  if (actions.length === 0) return;
  if (Platform.OS === 'ios') {
    const destructive = actions.flatMap((a, i) => (a.destructive ? [i] : []));
    ActionSheetIOS.showActionSheetWithOptions(
      { title, options: [...actions.map((a) => a.label), 'Cancel'], cancelButtonIndex: actions.length, destructiveButtonIndex: destructive },
      (i) => actions[i]?.onPress(),
    );
  } else {
    Alert.alert(title ?? '', undefined, [...actions.map((a) => ({ text: a.label, style: a.destructive ? ('destructive' as const) : ('default' as const), onPress: a.onPress })), { text: 'Cancel', style: 'cancel' }], {
      cancelable: true,
    });
  }
}

/** "Delete X?" confirmation; `onConfirm` runs only when the user taps Delete. */
export function confirmDelete(title: string, message: string | undefined, onConfirm: () => void) {
  Alert.alert(title, message, [
    { text: 'Cancel', style: 'cancel' },
    { text: 'Delete', style: 'destructive', onPress: onConfirm },
  ]);
}
