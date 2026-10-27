import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import * as ImagePicker from 'expo-image-picker';
import { Alert } from 'react-native';

/** Phone photos are 4000+ px and several MB; 2048 px is plenty for trail photos and uploads fast on mobile data. */
const MAX_EDGE_PX = 2048;

export type PreparedPhoto = { uri: string; fileName: string };

/**
 * Takes or picks one photo and returns a resized JPEG ready to upload, or null when cancelled.
 * Re-encoding to JPEG also converts HEIC and strips EXIF (including GPS of where it was taken).
 */
export async function takeOrPickPhoto(source: 'camera' | 'library'): Promise<PreparedPhoto | null> {
  const permission =
    source === 'camera' ? await ImagePicker.requestCameraPermissionsAsync() : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    Alert.alert('Permission needed', source === 'camera' ? 'Allow camera access to take trail photos.' : 'Allow photo access to add trail photos.');
    return null;
  }

  const options: ImagePicker.ImagePickerOptions = { mediaTypes: ['images'], quality: 1, exif: false };
  let result: ImagePicker.ImagePickerResult;
  try {
    result = source === 'camera' ? await ImagePicker.launchCameraAsync(options) : await ImagePicker.launchImageLibraryAsync(options);
  } catch (e) {
    // e.g. no camera on the simulator
    Alert.alert('Could not open the camera', (e as Error).message);
    return null;
  }
  if (result.canceled) return null;

  const asset = result.assets[0];
  const longest = Math.max(asset.width, asset.height);
  const context = ImageManipulator.manipulate(asset.uri);
  if (longest > MAX_EDGE_PX) {
    context.resize(asset.width >= asset.height ? { width: MAX_EDGE_PX } : { height: MAX_EDGE_PX });
  }
  const image = await context.renderAsync();
  const saved = await image.saveAsync({ format: SaveFormat.JPEG, compress: 0.8 });
  return { uri: saved.uri, fileName: `photo-${Date.now()}.jpg` };
}

/** Lets the user choose camera or library, then prepares the photo. */
export function choosePhotoSource(): Promise<PreparedPhoto | null> {
  return new Promise((resolve) =>
    Alert.alert('Add a photo', undefined, [
      { text: 'Take photo', onPress: () => void takeOrPickPhoto('camera').then(resolve) },
      { text: 'Choose from library', onPress: () => void takeOrPickPhoto('library').then(resolve) },
      { text: 'Cancel', style: 'cancel', onPress: () => resolve(null) },
    ]),
  );
}
