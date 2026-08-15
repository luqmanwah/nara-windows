using System.Security.Cryptography;
using System.Text.Json;

namespace Nara.ApprovalContracts;

public static class ApprovalCryptography
{
    public static readonly JsonSerializerOptions CompactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static byte[] SerializeUnsignedReceipt(ApprovalReceiptV3 receipt) =>
        JsonSerializer.SerializeToUtf8Bytes(ToUnsigned(receipt), CompactJson);

    public static byte[] SerializeUnsignedConsent(ConsentRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(ToUnsigned(request), CompactJson);

    public static string SignReceipt(ApprovalReceiptV3 receipt, ECDsa signer) =>
        Sign(SerializeUnsignedReceipt(receipt), signer);

    public static string SignConsent(ConsentRequest request, ECDsa signer) =>
        Sign(SerializeUnsignedConsent(request), signer);

    public static bool VerifyReceipt(ApprovalReceiptV3 receipt, string publicKeySpkiBase64) =>
        Verify(SerializeUnsignedReceipt(receipt), receipt.SignatureBase64, publicKeySpkiBase64);

    public static bool VerifyConsent(ConsentRequest request, string publicKeySpkiBase64) =>
        Verify(SerializeUnsignedConsent(request), request.RequestSignatureBase64, publicKeySpkiBase64);

    public static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string Sign(byte[] content, ECDsa signer) =>
        Convert.ToBase64String(signer.SignData(
            content,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));

    private static bool Verify(byte[] content, string signatureBase64, string publicKeySpkiBase64)
    {
        try
        {
            byte[] signature = Convert.FromBase64String(signatureBase64);
            byte[] publicKey = Convert.FromBase64String(publicKeySpkiBase64);
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
            return bytesRead == publicKey.Length
                && verifier.KeySize == 256
                && verifier.VerifyData(
                    content,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }

    private static ApprovalReceiptUnsigned ToUnsigned(ApprovalReceiptV3 receipt) => new(
        receipt.SchemaVersion,
        receipt.ApprovalId,
        receipt.SessionNonce,
        receipt.InstallationId,
        receipt.Scope,
        receipt.ActorType,
        receipt.ApprovalMode,
        receipt.ConsentRequestSha256,
        receipt.ActionPlanSha256,
        receipt.PlaybookSha256,
        receipt.PlaybookSignatureKeyId,
        receipt.ApprovedActionIds,
        receipt.IssuedAtUtc,
        receipt.ExpiresAtUtc,
        receipt.SingleUse,
        receipt.ConsentTextVersion,
        receipt.LocalApprovalKeyId,
        receipt.SignatureAlgorithm);

    private static ConsentRequestUnsigned ToUnsigned(ConsentRequest request) => new(
        request.SchemaVersion,
        request.RequestId,
        request.InstallationId,
        request.BrokerKeyId,
        request.Status,
        request.CreatedAtUtc,
        request.ExpiresAtUtc,
        request.ActionPlanSha256,
        request.PlaybookSha256,
        request.PlaybookSignatureKeyId,
        request.ApprovalMode,
        request.Actions,
        request.ConsentStatement,
        request.SignatureAlgorithm);

    private sealed record ApprovalReceiptUnsigned(
        string SchemaVersion,
        string ApprovalId,
        string SessionNonce,
        string InstallationId,
        string Scope,
        string ActorType,
        string ApprovalMode,
        string ConsentRequestSha256,
        string ActionPlanSha256,
        string PlaybookSha256,
        string PlaybookSignatureKeyId,
        string[] ApprovedActionIds,
        string IssuedAtUtc,
        string ExpiresAtUtc,
        bool SingleUse,
        string ConsentTextVersion,
        string LocalApprovalKeyId,
        string SignatureAlgorithm);

    private sealed record ConsentRequestUnsigned(
        string SchemaVersion,
        string RequestId,
        string InstallationId,
        string BrokerKeyId,
        string Status,
        string CreatedAtUtc,
        string ExpiresAtUtc,
        string ActionPlanSha256,
        string PlaybookSha256,
        string PlaybookSignatureKeyId,
        string ApprovalMode,
        ConsentAction[] Actions,
        string ConsentStatement,
        string SignatureAlgorithm);
}
