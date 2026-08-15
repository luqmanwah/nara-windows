using System.Security.Cryptography;
using Nara.ApprovalContracts;

namespace Nara.ApprovalBroker;

internal static class SimulationKeyStore
{
    internal static (FakeWindowsState State, FakeApprovalKey Key) Initialize(FakeWindowsState template, DateTimeOffset nowUtc)
    {
        BrokerJson.Require(template.SchemaVersion == "1.0.0", "Unsupported fake-state schema version.");
        BrokerJson.Require(template.Adapter == "fake-windows", "Approval Broker simulation accepts only fake-windows state.");

        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string installationId = Guid.NewGuid().ToString();
        string keyId = Guid.NewGuid().ToString();
        string publicKey = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
        string privateKey = Convert.ToBase64String(signer.ExportPkcs8PrivateKey());

        template.InstallationId = installationId;
        template.ApprovalKeyId = keyId;
        template.ApprovalPublicKeySpkiBase64 = publicKey;

        FakeApprovalKey key = new()
        {
            SchemaVersion = "1.0.0",
            Protection = "test-only-plaintext",
            KeyId = keyId,
            InstallationId = installationId,
            Algorithm = "ecdsa-p256-sha256",
            CreatedAtUtc = BrokerJson.Utc(nowUtc),
            PublicKeySpkiBase64 = publicKey,
            PrivateKeyPkcs8Base64 = privateKey
        };
        return (template, key);
    }

    internal static ECDsa OpenAndValidate(FakeApprovalKey key, string expectedInstallationId, string expectedKeyId)
    {
        BrokerJson.Require(key.SchemaVersion == "1.0.0", "Unsupported simulation-key schema version.");
        BrokerJson.Require(key.Protection == "test-only-plaintext", "Only a test-only plaintext key is accepted by the simulation broker.");
        BrokerJson.Require(key.Algorithm == "ecdsa-p256-sha256", "Simulation-key algorithm is invalid.");
        BrokerJson.Require(key.InstallationId == expectedInstallationId, "Simulation key belongs to another installation.");
        BrokerJson.Require(key.KeyId == expectedKeyId, "Simulation key ID does not match the broker request.");

        try
        {
            ECDsa signer = ECDsa.Create();
            byte[] privateKey = Convert.FromBase64String(key.PrivateKeyPkcs8Base64);
            signer.ImportPkcs8PrivateKey(privateKey, out int bytesRead);
            BrokerJson.Require(bytesRead == privateKey.Length && signer.KeySize == 256, "Simulation private key is invalid.");
            BrokerJson.Require(
                Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo()) == key.PublicKeySpkiBase64,
                "Simulation public and private keys do not match.");
            return signer;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidDataException("Simulation private key material is invalid.", exception);
        }
    }
}
