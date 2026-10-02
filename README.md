# Certificate Validity and Logging in Digital Signing

[![Product Page](https://img.shields.io/badge/Product%20Page-2865E0?style=for-the-badge&logo=appveyor&logoColor=white)](https://github.com/groupdocs-signature/GroupDocs.Signature-Docs)
[![Docs](https://img.shields.io/badge/Docs-2865E0?style=for-the-badge&logo=Hugo&logoColor=white)](https://docs.groupdocs.com/signature/net/)
[![Blog](https://img.shields.io/badge/Blog-2865E0?style=for-the-badge&logo=WordPress&logoColor=white)](https://blog.groupdocs.com/categories/groupdocs.signature-product-family/)
[![Free Support](https://img.shields.io/badge/Free%20Support-2865E0?style=for-the-badge&logo=Discourse&logoColor=white)](https://forum.groupdocs.com/c/signature/13)
[![Temporary License](https://img.shields.io/badge/Temporary%20License-2865E0?style=for-the-badge&logo=rocket&logoColor=white)](https://purchase.groupdocs.com/temp-license/100124)

## 📖 About This Repository

`digital-signing-certificate-validity-dotnet` is a runnable .NET 8 console sample covering three behaviour changes that arrived in GroupDocs.Signature 26.9: PDF digital signatures now default to SHA-256, an expired or not-yet-valid certificate is rejected unless you allow it explicitly, and `SignatureSettings.LogLevel` actually filters. Each one is demonstrated against a real PDF, with the rejection path shown deliberately rather than described.

The sample ships no private key at all. `TestCertificates.cs` builds three self-signed PFXs in memory at run time - one valid, one expired last year, one valid only from next year - so the demo keeps working whatever today's date is and there is nothing sensitive in the repository.

## The Challenge

Digital signing has three quiet failure modes, and all three produce a file that looks signed.

The first is the digest. Before 26.9 PDF signatures were written with SHA-1, which current validators flag or reject outright. A pipeline could run for years producing signatures that an auditor would later refuse.

The second is certificate validity. Signing with an expired certificate succeeds at the API level and fails at every validator that opens the result, so the problem surfaces at the recipient rather than at the source. The same applies to a certificate whose validity has not started yet, which usually means the machine clock is wrong.

The third is logging. `SignatureSettings` has taken a logger for a long time, but before 26.9 the level was not applied: every message went to it regardless, so a production service either drowned in trace output or turned logging off entirely.

**What is GroupDocs.Signature for .NET?**

A signing library for PDF, Word, Excel, PowerPoint and image formats, covering digital certificates, text, barcode, QR-code, image and stamp signatures through one `Signature` object. The parts this sample uses:

- `DigitalSignOptions` with `HashAlgorithm`, `AllowExpired` and `AllowNotYetValid`
- `DigitalVerifyOptions`, which since 26.9 checks the signature cryptographically rather than only matching criteria
- `SignatureSettings` with an `ILogger` and a `LogLevel` flags value
- `SignResult.Succeeded`, the list that tells you what was actually written

## Prerequisites

- **.NET SDK 8.0** - the project targets `net8.0`
- **GroupDocs.Signature 26.9.0** - pinned in `DigitalSigningOptionsDemo.csproj`; the behaviour below is specific to 26.9 and later
- **No certificate needed** - the sample generates its own; bring your own CA certificate for production work
- **Licence (optional)** - without one the run is in evaluation mode and the library adds its own marks

## Repository Structure

```
digital-signing-certificate-validity-dotnet/
│
├── Program.cs
├── CollectingLogger.cs
├── TestCertificates.cs
├── DigitalSigningOptionsDemo.csproj
├── documents/
│   └── document.pdf
└── Result/
    ├── signed-sha256.pdf
    ├── signed-sha384.pdf
    ├── signed-sha512.pdf
    ├── signed-expired-allowed.pdf
    ├── signed-not-yet-valid-allowed.pdf
    └── signed-log-levels.pdf
```

- **Program.cs** - the six demonstrations, run in sequence with a pass/fail exit code
- **CollectingLogger.cs** - an `ILogger` that counts errors, warnings and traces so the log-level difference is measurable
- **TestCertificates.cs** - builds self-signed PFXs in memory; no key file is committed
- **documents/document.pdf** - the input
- **Result/** - one signed PDF per demonstration

## Code Examples

### Signs a PDF document with a digital signature that uses the given hash algorithm

Since 26.9 the default digest for PDF digital signatures is SHA-256, written in the `adbe.pkcs7.detached` format that current validators expect. `Sha384` and `Sha512` are there when a policy asks for a stronger digest; `Sha1` remains only for legacy validators.

```csharp
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
```

The certificate comes from a `MemoryStream` here because the sample generates it; a path works the same way. A time stamp, when you add one, uses the same digest as the signature.

### Checks that the digital signatures of a PDF document are intact

`DigitalVerifyOptions` with no criteria set used to mean "nothing to check". Since 26.9 it means a full cryptographic check, so a document modified after signing comes back invalid.

```csharp
using var signature = new Signature(signedPath);

VerificationResult result = signature.Verify(new DigitalVerifyOptions());
return result.IsValid;
```

Add `SubjectName`, `IssuerName` or `Reason` when you also need to check who signed rather than only that the content is intact.

### Tries to sign a PDF document with an expired certificate using the default settings

This is the behaviour change most likely to break an existing pipeline, which is why the sample runs it on purpose and prints the message.

```csharp
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
```

Nothing is signed and nothing is saved. The message names the certificate and the property that would allow it, so the fix is discoverable from the error alone - which is the point of failing here rather than letting the recipient find out.

### Signs a PDF document with an expired certificate by allowing it explicitly

When you genuinely need it - reproducing an old signature, testing against an archived certificate - the escape hatch is one property.

```csharp
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
```

The document is signed and a warning goes to the logger. Validators will still report the signature as invalid - `AllowExpired` changes what GroupDocs.Signature permits, not what the certificate is worth.

### Signs a PDF document with a certificate whose validity period has not started yet

`AllowNotYetValid` is the separate flag for the other end of the validity window, and the two do not imply each other.

```csharp
var options = new DigitalSignOptions(certificate)
{
    Password = TestCertificates.Password,
    AllowNotYetValid = true
};

SignResult result = signature.Sign(outputPath, options);
return result.Succeeded.Count;
```

I have hit this twice, and both times the certificate was fine and the build agent's clock was a day ahead. Before reaching for this one, check the machine clock. A certificate that is not valid yet is usually either issued for a later start date or being read by a host whose time is wrong, and the second case is worth fixing rather than overriding.

### Signs the same document under three log levels and counts the messages of each kind

The log-level fix is hard to see without counting, so the sample signs the same document three times with a counting logger and prints the totals.

```csharp
var levels = new Dictionary<string, LogLevel>
{
    ["None"] = LogLevel.None,
    ["Warning | Error"] = LogLevel.Warning | LogLevel.Error,
    ["All"] = LogLevel.All
};
```

Each iteration builds a fresh logger and settings, signs with an allowed expired certificate so there is exactly one warning to observe, and reports what arrived:

```csharp
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
```

`LogLevel.None` yields zero messages, `Warning | Error` keeps the one warning, and `All` adds a trace per step. Before 26.9 all three produced the same output.

### Does the log level change what my code receives?

No. `LogLevel` filters what reaches the `ILogger`; it has no effect on exceptions. An expired certificate without `AllowExpired` still throws `GroupDocsSignatureException` at `LogLevel.None`, and the logger simply never hears about it. Treat the two as separate channels: exceptions for control flow, the logger for diagnostics.

### Wiring the library into your own logging

`CollectingLogger` in this repository counts messages, but the same interface is how you route GroupDocs.Signature into Serilog, NLog or Application Insights:

```csharp
public void Warning(string message)
{
    Warnings++;
    WarningMessages.Add(message);
}
```

Three methods - `Error`, `Warning`, `Trace` - and the level decides which of them the library calls.

## Related Topics to Explore

If you're working with digital certificates in GroupDocs.Signature, the following articles may be helpful:

* **Step-by-step use case guide in the documentation** - the six demonstrations with a decision table for the validity flags: [Read the article →](https://docs.groupdocs.com/signature/net/use-cases/certificate-validity-hash-and-logging/)

* **In-depth blog article about this project** - what the 26.9 changes mean for a pipeline that has been signing for years: [Read the article →](https://blog.groupdocs.com/signature/certificate-validity-hash-and-logging-net/)

* **Sign Document with Digital Signature** - the reference for `DigitalSignOptions` and its certificate sources: [Read the article →](https://docs.groupdocs.com/signature/net/sign-document-with-digital-signature/)

* **Verify Digital Signatures in the Document** - the verification side, including criteria-based checks: [Read the article →](https://docs.groupdocs.com/signature/net/verify-digital-signatures-in-the-document/)

## 🏷️ Keywords

`digital signature`, `certificate validity`, `allowexpired`, `allownotyetvalid`, `sha-256`, `hash algorithm`, `pdf signing`, `groupdocs signature`, `dotnet signing`, `log level`, `ilogger`, `signaturesettings`, `digitalsignoptions`, `digitalverifyoptions`, `expired certificate`, `adbe.pkcs7.detached`, `net8`, `certificate verification`, `signing diagnostics`, `26.9`, `self-signed certificate`, `pdf validator`
