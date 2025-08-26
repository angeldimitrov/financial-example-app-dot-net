using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace FinanceApp.Web.Services;

/// <summary>
/// Service for comprehensive input sanitization with focus on XSS prevention
/// 
/// Security Features:
/// - HTML tag removal and encoding
/// - Script injection prevention
/// - Dangerous character neutralization
/// - German character preservation (ä, ö, ü, ß, €)
/// 
/// German BWA Context:
/// Sanitizes user input while preserving German business terminology
/// and financial data formatting used in BWA reports
/// </summary>
public interface IInputSanitizationService
{
    string SanitizeInput(string input);
    string SanitizeHtml(string input);
    string SanitizeFileName(string fileName);
    string SanitizeBwaCategory(string category);
    bool ContainsScriptInjection(string input);
    string SanitizeTransactionInput(string? input, string inputType);
    string CreateSafeDisplayText(string? input);
    string SanitizeNumericInput(string input);
}