using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Demo.DigitalSigningOptions;

// Creates throwaway self-signed certificates in memory, so the sample ships no private key
// and keeps working whatever today's date is. Use certificates from your CA in production.
internal static class TestCertificates
{
    public const string Password = "1234567890";

    public static byte[] CreatePfx(
        string subject, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=" + subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

        using X509Certificate2 certificate = request.CreateSelfSigned(notBefore, notAfter);
        return certificate.Export(X509ContentType.Pfx, Password);
    }
}
