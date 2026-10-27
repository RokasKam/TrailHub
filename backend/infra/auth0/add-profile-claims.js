/**
 * Auth0 Action — trigger: Login / Post Login.
 * Paste into Auth0 Dashboard → Actions → Library → Create Action → "Build from scratch",
 * then deploy it and drag it into Actions → Triggers → post-login.
 *
 * Access tokens only contain `sub` by default. TrailHub's Profile service uses these claims to
 * name a new profile. Custom claims must be namespaced URLs; the namespace must match the
 * backend's Auth0:ClaimsNamespace setting (default https://trailhub.app/).
 */
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://trailhub.app/';
  const { email, name, nickname } = event.user;

  if (email) api.accessToken.setCustomClaim(`${namespace}email`, email);
  if (name || nickname) api.accessToken.setCustomClaim(`${namespace}name`, name || nickname);
};
