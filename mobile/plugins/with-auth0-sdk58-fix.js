// react-native-auth0's config plugin inserts an `application(_:open:options:)` override at the
// "Extension point for config-plugins" comment. Since Expo SDK 58 that comment sits inside
// `ReactNativeDelegate`, which has no such method, so the Swift build fails. The override isn't
// needed: SDK 58's scene delegate already forwards opened URLs to RCTLinkingManager, and Auth0
// on iOS receives its callback straight from ASWebAuthenticationSession.
// Remove this plugin once react-native-auth0 supports the SDK 58 AppDelegate template.
const { withAppDelegate } = require('expo/config-plugins');

const GENERATED_BLOCK = /\/\/ @generated begin react-native-auth0-linking-swift[\s\S]*?\/\/ @generated end react-native-auth0-linking-swift\n?/;

module.exports = function withAuth0Sdk58Fix(config) {
  return withAppDelegate(config, (mod) => {
    if (mod.modResults.language === 'swift') {
      mod.modResults.contents = mod.modResults.contents.replace(GENERATED_BLOCK, '');
    }
    return mod;
  });
};
