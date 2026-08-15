using System.Security.Cryptography;
using System.Text.Json;
using Nara.ApprovalContracts;

namespace Nara.ApprovalBroker;

internal static class BrokerTrustVerifier
{
    private const string RootKeyId = "nara-stage2-root-2026";
    private const string RootPublicKeySpkiBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEzraL10YM3D1vLyIQ3ya92f1QlG9XL/841cfuTcdkqlCJ7cK7PT5ThJP0wHVKLS0I9F95A/dp+eLxiIU5FZppZw==";

    internal static string Verify(
        byte[] playbookBytes,
        byte[] playbookSignatureBytes,
        byte[] trustManifestBytes,
        byte[] trustSignatureBytes,
        DateTimeOffset nowUtc)
    {
        using JsonDocument trustSignatureDocument = JsonDocument.Parse(trustSignatureBytes);
        JsonElement trustSignature = trustSignatureDocument.RootElement;
        BrokerJson.Require(RequiredString(trustSignature, "rootKeyId") == RootKeyId, "Broker does not trust the manifest root key.");
        BrokerJson.Require(RequiredString(trustSignature, "algorithm") == "ecdsa-p256-sha256", "Broker trust signature algorithm is invalid.");
        BrokerJson.Require(
            RequiredString(trustSignature, "contentSha256") == ApprovalCryptography.Sha256(trustManifestBytes),
            "Broker trust-manifest hash does not match.");
        VerifyEcdsa(
            trustManifestBytes,
            RequiredString(trustSignature, "signatureBase64"),
            RootPublicKeySpkiBase64,
            "Broker trust-manifest signature is invalid.");

        using JsonDocument manifestDocument = JsonDocument.Parse(trustManifestBytes);
        JsonElement manifest = manifestDocument.RootElement;
        BrokerJson.Require(RequiredString(manifest, "schemaVersion") == "1.0.0", "Broker trust-manifest schema is unsupported.");
        BrokerJson.Require(RequiredString(manifest, "manifestId") == "nara-playbook-trust", "Broker trust-manifest ID is invalid.");
        BrokerJson.Require(DateTimeOffset.TryParse(RequiredString(manifest, "issuedAtUtc"), out DateTimeOffset issuedAt), "Broker trust-manifest issue time is invalid.");
        BrokerJson.Require(DateTimeOffset.TryParse(RequiredString(manifest, "expiresAtUtc"), out DateTimeOffset expiresAt), "Broker trust-manifest expiry is invalid.");
        BrokerJson.Require(nowUtc >= issuedAt && nowUtc <= expiresAt, "Broker trust manifest is outside its validity window.");

        using JsonDocument playbookSignatureDocument = JsonDocument.Parse(playbookSignatureBytes);
        JsonElement playbookSignature = playbookSignatureDocument.RootElement;
        string signingKeyId = RequiredString(playbookSignature, "keyId");
        BrokerJson.Require(
            RequiredString(playbookSignature, "contentSha256") == ApprovalCryptography.Sha256(playbookBytes),
            "Broker playbook-signature hash does not match.");
        BrokerJson.Require(RequiredString(playbookSignature, "algorithm") == "ecdsa-p256-sha256", "Broker playbook-signature algorithm is invalid.");

        HashSet<string> revoked = manifest.GetProperty("revokedKeyIds")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        JsonElement? key = manifest.GetProperty("keys")
            .EnumerateArray()
            .Cast<JsonElement?>()
            .SingleOrDefault(item => item is not null && RequiredString(item.Value, "keyId") == signingKeyId);
        BrokerJson.Require(key is not null, "Broker could not resolve the playbook signing key.");
        JsonElement resolvedKey = key.Value;
        BrokerJson.Require(RequiredString(resolvedKey, "status") == "active", "Broker refuses a non-active playbook signing key.");
        BrokerJson.Require(!revoked.Contains(signingKeyId), "Broker refuses a revoked playbook signing key.");
        BrokerJson.Require(DateTimeOffset.TryParse(RequiredString(resolvedKey, "notBeforeUtc"), out DateTimeOffset notBefore), "Broker signing-key start time is invalid.");
        BrokerJson.Require(DateTimeOffset.TryParse(RequiredString(resolvedKey, "notAfterUtc"), out DateTimeOffset notAfter), "Broker signing-key expiry is invalid.");
        BrokerJson.Require(nowUtc >= notBefore && nowUtc <= notAfter, "Broker signing key is outside its validity window.");
        VerifyEcdsa(
            playbookBytes,
            RequiredString(playbookSignature, "signatureBase64"),
            RequiredString(resolvedKey, "publicKeySpkiBase64"),
            "Broker playbook signature is invalid.");
        return signingKeyId;
    }

    private static void VerifyEcdsa(byte[] content, string signatureBase64, string publicKeyBase64, string message)
    {
        try
        {
            byte[] signature = Convert.FromBase64String(signatureBase64);
            byte[] publicKey = Convert.FromBase64String(publicKeyBase64);
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
            BrokerJson.Require(bytesRead == publicKey.Length && verifier.KeySize == 256, "Broker public key is invalid.");
            BrokerJson.Require(
                verifier.VerifyData(content, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence),
                message);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidDataException("Broker cryptographic material is invalid.", exception);
        }
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        string? value = element.GetProperty(propertyName).GetString();
        return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidDataException($"Broker trust field is empty: {propertyName}");
    }
}
