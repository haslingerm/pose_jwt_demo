import { jwtDecode } from 'jwt-decode';
import { z } from 'zod';

/**
 * Reads the claims (payload) of our access token.
 *
 * Important: this does NOT validate the token - the frontend does not (and must not!) know the signing key.
 * Anybody can create a token with arbitrary content, so the claims are only used to improve the UI
 * (show the username, hide buttons the user may not use anyway, ...).
 * The backend validates the token on every request - that is where the actual security happens.
 */
export function decodeAccessToken(accessToken: string): IAccessTokenClaims | null {
  try {
    const result = AccessTokenClaimsWire.safeParse(jwtDecode(accessToken));
    return result.success ? result.data : null;
  } catch {
    // jwtDecode throws if the token is malformed
    return null;
  }
}

const AccessTokenClaimsWire = z.object({
  sub: z.string(),
  name: z.string(),
  // a JWT claim with a single value is a string, with multiple values it is an array
  role: z.union([z.string(), z.array(z.string())])
    .transform(role => Array.isArray(role) ? role : [role])
});

export type IAccessTokenClaims = z.infer<typeof AccessTokenClaimsWire>;
