import * as DocumentPicker from 'expo-document-picker';
import { Alert } from 'react-native';

export type PickedFile = DocumentPicker.DocumentPickerAsset;

let inFlight: Promise<PickedFile | null> | null = null;

/**
 * Opens the system file picker for a GPX file. iOS allows only one picker at a time, so a second
 * call while one is open (e.g. a double tap) reuses the pending pick instead of throwing
 * "Different document picking in progress". Resolves to null when cancelled or on error.
 */
export function pickGpxFile(): Promise<PickedFile | null> {
  inFlight ??= DocumentPicker.getDocumentAsync({
    // GPX has no registered type on many devices, so allow any file and let the server validate it.
    type: ['application/gpx+xml', 'application/xml', 'text/xml', '*/*'],
    copyToCacheDirectory: true,
  })
    .then((result) => (result.canceled ? null : result.assets[0]))
    .catch((error: Error) => {
      Alert.alert('Could not open files', error.message);
      return null;
    })
    .finally(() => {
      inFlight = null;
    });
  return inFlight;
}
