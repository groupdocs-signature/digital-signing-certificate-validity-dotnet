// Topic: Digital signing in GroupDocs.Signature 26.9 - certificate validity checks
// (AllowExpired, AllowNotYetValid), SHA-2 digests for PDF signatures, and log levels.
// Uses GroupDocs.Signature for .NET: DigitalSignOptions, DigitalVerifyOptions and
// SignatureSettings with a logger.

using GroupDocs.Signature;
using GroupDocs.Signature.Domain;
using GroupDocs.Signature.Logging;
using GroupDocs.Signature.Options;

namespace Demo.DigitalSigningOptions;

internal static class Program
{
    private const string DocsFolder = "documents";
    private const string ResultFolder = "Result";

    private static readonly string SourcePdf = Path.Combine(DocsFolder, "document.pdf");

    private static int Main()
    {
        Directory.CreateDirectory(DocsFolder);
        Directory.CreateDirectory(ResultFolder);
        ApplyLicense();

        if (!File.Exists(SourcePdf))
        {
            Console.Error.WriteLine(
                $"Missing source document: {Path.GetFullPath(SourcePdf)}");
            return 1;
        }

        // Test certificates: valid now, expired last year, and valid only from next year.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        byte[] validPfx = TestCertificates.CreatePfx(
            "Valid Signer", now.AddDays(-1), now.AddYears(1));
        byte[] expiredPfx = TestCertificates.CreatePfx(
            "Expired Signer", now.AddYears(-2), now.AddYears(-1));
        byte[] futurePfx = TestCertificates.CreatePfx(
            "Future Signer", now.AddYears(1), now.AddYears(2));

        int failures = 0;

        Console.WriteLine("1. Hash algorithms");
        var algorithms = new[]
        {
            HashAlgorithm.Sha256, HashAlgorithm.Sha384, HashAlgorithm.Sha512
        };
        foreach (HashAlgorithm algorithm in algorithms)
        {
            string name = algorithm.ToString().ToLowerInvariant();
            string output = Path.Combine(ResultFolder, $"signed-{name}.pdf");
            SignWithHashAlgorithm(SourcePdf, validPfx, algorithm, output);
            bool valid = VerifyPdfSignature(output);
            Console.WriteLine(
                $"   {algorithm}: {Path.GetFileName(output)}, valid: {valid}");
            failures += valid ? 0 : 1;
        }

        Console.WriteLine("2. Expired certificate with the default settings");
        string rejected = Path.Combine(ResultFolder, "not-signed.pdf");
        bool signedExpired = SignWithExpiredCertificate(SourcePdf, expiredPfx, rejected);
        failures += signedExpired ? 1 : 0;

        Console.WriteLine("3. Expired certificate with AllowExpired");
        string allowed = Path.Combine(ResultFolder, "signed-expired-allowed.pdf");
        failures += SignWithAllowExpired(SourcePdf, expiredPfx, allowed) == 1 ? 0 : 1;

        Console.WriteLine("4. Not-yet-valid certificate with AllowNotYetValid");
        string early = Path.Combine(ResultFolder, "signed-not-yet-valid-allowed.pdf");
        failures += SignWithAllowNotYetValid(SourcePdf, futurePfx, early) == 1 ? 0 : 1;

        Console.WriteLine("5. Log levels");
        int messagesWithNone = CompareLogLevels(SourcePdf, expiredPfx);
        failures += messagesWithNone == 0 ? 0 : 1;

        Console.WriteLine($"Results: {Path.GetFullPath(ResultFolder)}");
        return failures == 0 ? 0 : 2;
    }

    private static void ApplyLicense()
    {
        // Point this at your .lic file to remove evaluation limits.
        // Get a free temporary licence: https://purchase.groupdocs.com/temporary-license
        const string licensePath = "REPLACE_WITH_YOUR_LICENSE_PATH";
        if (File.Exists(licensePath))
        {
            new License().SetLicense(licensePath);
            Console.WriteLine("[license] applied");
        }
        else
        {
            Console.WriteLine("[license] no licence set - running in evaluation mode");
        }
    }

    /// <summary>
    /// Signs a PDF document with a digital signature that uses the given hash
    /// algorithm.
    /// </summary>
    /// <remarks>
    /// Sets <see cref="SignOptions.HashAlgorithm"/> on a
    /// <see cref="DigitalSignOptions"/> that reads the certificate from a stream, and
    /// calls <c>Sign</c>. Since GroupDocs.Signature 26.9, PDF digital signatures use
    /// SHA-256 by default, in the <c>adbe.pkcs7.detached</c> format that current
    /// validators expect; earlier versions wrote SHA-1. <c>Sha384</c> and <c>Sha512</c>
    /// are available when a policy asks for a stronger digest, and a time stamp, when
    /// you add one, uses the same digest. <c>Sha1</c> remains only for legacy
    /// validators. Writes the signed PDF to <paramref name="outputPath"/> and returns
    /// the number of signatures added.
    /// </remarks>
    public static int SignWithHashAlgorithm(
        string sourcePath, byte[] pfx, HashAlgorithm algorithm, string outputPath)
    {
        using var signature = new Signature(sourcePath);
        using var certificate = new MemoryStream(pfx);

        var options = new DigitalSignOptions(certificate)
        {
            Password = TestCertificates.Password,
            HashAlgorithm = algorithm,
            Reason = "Approved",
            Location = "Head office"
        };

        SignResult result = signature.Sign(outputPath, options);
        return result.Succeeded.Count;
    }

    /// <summary>
    /// Checks that the digital signatures of a PDF document are intact.
    /// </summary>
    /// <remarks>
    /// Calls <c>Verify</c> with a <see cref="DigitalVerifyOptions"/> that sets no
    /// criteria. Since GroupDocs.Signature 26.9, every PDF digital signature is checked
    /// cryptographically, so a document that was changed after signing is reported as
    /// not valid; earlier versions only compared the criteria. Add criteria such as
    /// <c>SubjectName</c>, <c>IssuerName</c> or <c>Reason</c> to also check who signed.
    /// Returns <c>true</c> when the verification succeeds.
    /// </remarks>
    public static bool VerifyPdfSignature(string signedPath)
    {
        using var signature = new Signature(signedPath);

        VerificationResult result = signature.Verify(new DigitalVerifyOptions());
        return result.IsValid;
    }

    /// <summary>
    /// Tries to sign a PDF document with an expired certificate using the default
    /// settings.
    /// </summary>
    /// <remarks>
    /// Since GroupDocs.Signature 26.9, <c>Sign</c> rejects a certificate whose validity
    /// period has ended, or has not started, because validators report such a signature
    /// as not valid. It throws <see cref="GroupDocsSignatureException"/>, nothing is
    /// signed and nothing is saved, and the message names the certificate and the
    /// property that allows it. Catching it lets an application tell the user to renew
    /// the certificate instead of producing a document nobody can trust. Returns
    /// <c>false</c> when the signing is rejected.
    /// </remarks>
    public static bool SignWithExpiredCertificate(
        string sourcePath, byte[] expiredPfx, string outputPath)
    {
        using var signature = new Signature(sourcePath);
        using var certificate = new MemoryStream(expiredPfx);

        var options = new DigitalSignOptions(certificate)
        {
            Password = TestCertificates.Password
        };

        try
        {
            signature.Sign(outputPath, options);
            return true;
        }
        catch (GroupDocsSignatureException ex)
        {
            Console.WriteLine($"   Rejected: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Signs a PDF document with an expired certificate by allowing it explicitly.
    /// </summary>
    /// <remarks>
    /// Sets <see cref="DigitalSignOptions.AllowExpired"/> to <c>true</c>, which is
    /// useful, for example, to test with an old certificate. The document is signed,
    /// and GroupDocs.Signature writes a warning to the logger given in
    /// <see cref="SignatureSettings"/>: here a <see cref="ConsoleLogger"/> with
    /// <c>LogLevel.Warning | LogLevel.Error</c>, so only the warning appears.
    /// Validators still report the signature as not valid. Writes the signed PDF to
    /// <paramref name="outputPath"/> and returns the number of signatures added.
    /// </remarks>
    public static int SignWithAllowExpired(
        string sourcePath, byte[] expiredPfx, string outputPath)
    {
        var settings = new SignatureSettings(new ConsoleLogger())
        {
            LogLevel = LogLevel.Warning | LogLevel.Error
        };

        using var signature = new Signature(sourcePath, settings);
        using var certificate = new MemoryStream(expiredPfx);

        var options = new DigitalSignOptions(certificate)
        {
            Password = TestCertificates.Password,
            AllowExpired = true
        };

        SignResult result = signature.Sign(outputPath, options);
        return result.Succeeded.Count;
    }

    /// <summary>
    /// Signs a PDF document with a certificate whose validity period has not started
    /// yet.
    /// </summary>
    /// <remarks>
    /// Sets <see cref="DigitalSignOptions.AllowNotYetValid"/> to <c>true</c>. A
    /// certificate that is not valid yet was usually issued for a later date, or the
    /// computer's clock is wrong, so check the clock before allowing it. The two
    /// properties are independent: <c>AllowExpired</c> does not allow a certificate
    /// that is not valid yet. The document is signed and a warning is written to the
    /// logger. Writes the signed PDF to <paramref name="outputPath"/> and returns the
    /// number of signatures added.
    /// </remarks>
    public static int SignWithAllowNotYetValid(
        string sourcePath, byte[] futurePfx, string outputPath)
    {
        var settings = new SignatureSettings(new ConsoleLogger())
        {
            LogLevel = LogLevel.Warning | LogLevel.Error
        };

        using var signature = new Signature(sourcePath, settings);
        using var certificate = new MemoryStream(futurePfx);

        var options = new DigitalSignOptions(certificate)
        {
            Password = TestCertificates.Password,
            AllowNotYetValid = true
        };

        SignResult result = signature.Sign(outputPath, options);
        return result.Succeeded.Count;
    }

    /// <summary>
    /// Signs the same document under three log levels and counts the messages of each
    /// kind.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="SignatureSettings.LogLevel"/> with a custom
    /// <see cref="ILogger"/> (<see cref="CollectingLogger"/>). The values are flags:
    /// <c>LogLevel.None</c> logs nothing, <c>LogLevel.Warning | LogLevel.Error</c>
    /// keeps problems only, and <c>LogLevel.All</c>, the default, adds a trace message
    /// for each step. Before version 26.9 the level had no effect and every message was
    /// logged. Signing with an allowed expired certificate produces one warning, which
    /// makes the difference visible. Prints the counts per level and returns the number
    /// of messages logged with <c>LogLevel.None</c>, which is zero.
    /// </remarks>
    public static int CompareLogLevels(string sourcePath, byte[] expiredPfx)
    {
        var levels = new Dictionary<string, LogLevel>
        {
            ["None"] = LogLevel.None,
            ["Warning | Error"] = LogLevel.Warning | LogLevel.Error,
            ["All"] = LogLevel.All
        };

        int messagesWithNone = -1;
        foreach (KeyValuePair<string, LogLevel> level in levels)
        {
            var logger = new CollectingLogger();
            var settings = new SignatureSettings(logger) { LogLevel = level.Value };

            using (var signature = new Signature(sourcePath, settings))
            using (var certificate = new MemoryStream(expiredPfx))
            {
                var options = new DigitalSignOptions(certificate)
                {
                    Password = TestCertificates.Password,
                    AllowExpired = true
                };
                string output = Path.Combine(ResultFolder, "signed-log-levels.pdf");
                signature.Sign(output, options);
            }

            int total = logger.Errors + logger.Warnings + logger.Traces;
            Console.WriteLine(
                $"   {level.Key,-16}: {logger.Errors} errors, " +
                $"{logger.Warnings} warnings, {logger.Traces} traces");
            if (level.Value == LogLevel.None)
            {
                messagesWithNone = total;
            }
        }

        return messagesWithNone;
    }
}
