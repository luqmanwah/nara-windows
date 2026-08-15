using System.Security.Cryptography;

namespace Nara.PlaybookEngine;

internal static class TrustedPlaybookKeys
{
    internal const string DevelopmentRootKeyId = "nara-stage2-root-2026";

    private const string DevelopmentRootPublicKeySpkiBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEzraL10YM3D1vLyIQ3ya92f1QlG9XL/841cfuTcdkqlCJ7cK7PT5ThJP0wHVKLS0I9F95A/dp+eLxiIU5FZppZw==";

    internal static void VerifyManifest(byte[] manifestBytes, TrustManifestSignature signature)
    {
        JsonSupport.Require(signature.SchemaVersion == "1.0.0", "Unsupported trust-manifest signature schema version.");
        JsonSupport.Require(signature.RootKeyId == DevelopmentRootKeyId, "Trust manifest root key is not trusted.");
        JsonSupport.Require(signature.Algorithm == "ecdsa-p256-sha256", "Trust manifest signature algorithm is unsupported.");
        JsonSupport.Require(signature.ContentSha256 == JsonSupport.Sha256(manifestBytes), "Trust manifest content hash does not match.");
        VerifyEcdsa(
            manifestBytes,
            signature.SignatureBase64,
            DevelopmentRootPublicKeySpkiBase64,
            "Trust manifest cryptographic signature verification failed.");
    }

    internal static void ValidateManifest(TrustManifest manifest, DateTimeOffset nowUtc)
    {
        JsonSupport.Require(manifest.Keys is not null, "Trust manifest keys are required.");
        JsonSupport.Require(manifest.RevokedKeyIds is not null, "Trust manifest revocation list is required.");
        JsonSupport.Require(manifest.SchemaVersion == "1.0.0", "Unsupported trust-manifest schema version.");
        JsonSupport.Require(manifest.ManifestId == "nara-playbook-trust", "Trust manifest ID is invalid.");
        JsonSupport.Require(manifest.Environment == "development", "Engine 0.3.0 accepts only the development trust environment.");
        JsonSupport.Require(DateTimeOffset.TryParse(manifest.IssuedAtUtc, out DateTimeOffset issuedAt), "Trust manifest issue time is invalid.");
        JsonSupport.Require(DateTimeOffset.TryParse(manifest.ExpiresAtUtc, out DateTimeOffset expiresAt), "Trust manifest expiry time is invalid.");
        JsonSupport.Require(expiresAt > issuedAt, "Trust manifest expiry must be later than its issue time.");
        JsonSupport.Require(nowUtc >= issuedAt, "Trust manifest is not active yet.");
        JsonSupport.Require(nowUtc <= expiresAt, "Trust manifest has expired.");
        JsonSupport.Require(manifest.Keys.Length > 0, "Trust manifest has no signing keys.");
        JsonSupport.Require(
            manifest.Keys.Select(key => key.KeyId).Distinct(StringComparer.Ordinal).Count() == manifest.Keys.Length,
            "Trust manifest contains duplicate key IDs.");
        JsonSupport.Require(
            manifest.RevokedKeyIds.Distinct(StringComparer.Ordinal).Count() == manifest.RevokedKeyIds.Length,
            "Trust manifest contains duplicate revocation entries.");

        HashSet<string> knownKeyIds = manifest.Keys.Select(key => key.KeyId).ToHashSet(StringComparer.Ordinal);
        foreach (string revokedKeyId in manifest.RevokedKeyIds)
        {
            JsonSupport.Require(knownKeyIds.Contains(revokedKeyId), $"Revocation references an unknown key: {revokedKeyId}");
        }

        foreach (TrustKey key in manifest.Keys)
        {
            JsonSupport.Require(key is not null, "Trust manifest contains a null key.");
            JsonSupport.Require(!string.IsNullOrWhiteSpace(key.KeyId), "Trust key ID is required.");
            JsonSupport.Require(key.Purpose == "playbook-signing", $"Trust key purpose is invalid: {key.KeyId}");
            JsonSupport.Require(key.Algorithm == "ecdsa-p256-sha256", $"Trust key algorithm is invalid: {key.KeyId}");
            JsonSupport.Require(key.Status is "active" or "retired" or "revoked", $"Trust key status is invalid: {key.KeyId}");
            JsonSupport.Require(DateTimeOffset.TryParse(key.NotBeforeUtc, out DateTimeOffset notBefore), $"Trust key start time is invalid: {key.KeyId}");
            JsonSupport.Require(DateTimeOffset.TryParse(key.NotAfterUtc, out DateTimeOffset notAfter), $"Trust key expiry time is invalid: {key.KeyId}");
            JsonSupport.Require(notAfter > notBefore, $"Trust key validity window is invalid: {key.KeyId}");
            JsonSupport.Require(
                (key.Status == "revoked") == manifest.RevokedKeyIds.Contains(key.KeyId, StringComparer.Ordinal),
                $"Trust key status and revocation list disagree: {key.KeyId}");
            ValidatePublicKey(key.PublicKeySpkiBase64, key.KeyId);
        }
    }

    internal static void VerifyPlaybook(
        byte[] playbookBytes,
        PlaybookSignature signature,
        TrustManifest manifest,
        DateTimeOffset nowUtc)
    {
        JsonSupport.Require(signature.SchemaVersion == "1.0.0", "Unsupported playbook signature schema version.");
        JsonSupport.Require(signature.Algorithm == "ecdsa-p256-sha256", "Playbook signature algorithm is not supported.");
        JsonSupport.Require(signature.ContentSha256 == JsonSupport.Sha256(playbookBytes), "Playbook signature content hash does not match.");

        TrustKey? key = manifest.Keys.SingleOrDefault(item => item.KeyId == signature.KeyId);
        JsonSupport.Require(key is not null, "Playbook signing key is absent from the trust manifest.");
        JsonSupport.Require(key.Status == "active", "Playbook signing key is not active.");
        JsonSupport.Require(!manifest.RevokedKeyIds.Contains(key.KeyId, StringComparer.Ordinal), "Playbook signing key has been revoked.");
        JsonSupport.Require(DateTimeOffset.TryParse(key.NotBeforeUtc, out DateTimeOffset notBefore), "Playbook signing key start time is invalid.");
        JsonSupport.Require(DateTimeOffset.TryParse(key.NotAfterUtc, out DateTimeOffset notAfter), "Playbook signing key expiry time is invalid.");
        JsonSupport.Require(nowUtc >= notBefore && nowUtc <= notAfter, "Playbook signing key is outside its validity window.");
        VerifyEcdsa(
            playbookBytes,
            signature.SignatureBase64,
            key.PublicKeySpkiBase64,
            "Playbook cryptographic signature verification failed.");
    }

    private static void VerifyEcdsa(byte[] content, string signatureBase64, string publicKeyBase64, string failureMessage)
    {
        try
        {
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64);
            byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKeyBytes, out int bytesRead);
            JsonSupport.Require(bytesRead == publicKeyBytes.Length, "Trusted public key contains trailing data.");
            bool valid = verifier.VerifyData(
                content,
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            JsonSupport.Require(valid, failureMessage);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Cryptographic material is not valid Base64.", exception);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("Cryptographic material is invalid.", exception);
        }
    }

    private static void ValidatePublicKey(string publicKeyBase64, string keyId)
    {
        try
        {
            byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKeyBytes, out int bytesRead);
            JsonSupport.Require(bytesRead == publicKeyBytes.Length, $"Trust key contains trailing data: {keyId}");
            JsonSupport.Require(verifier.KeySize == 256, $"Trust key is not P-256: {keyId}");
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidDataException($"Trust public key is invalid: {keyId}", exception);
        }
    }
}
