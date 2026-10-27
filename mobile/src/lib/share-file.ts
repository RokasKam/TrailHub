import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';

/** Writes text to a temp file and opens the share sheet (save to Files, AirDrop, other apps). */
export async function shareTextFile(fileName: string, content: string, mimeType = 'application/gpx+xml') {
  const file = new File(Paths.cache, fileName);
  if (file.exists) file.delete();
  file.create();
  file.write(content);
  await Sharing.shareAsync(file.uri, { mimeType, UTI: 'com.topografix.gpx', dialogTitle: fileName });
}
