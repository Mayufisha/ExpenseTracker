using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ExpenseTracker.Services;

public static partial class PdfStatementParser
{
    public static Task<IReadOnlyList<ParsedStatementTransaction>> ParseAsync(
        string filePath,
        StatementAmountConvention amountConvention)
    {
        return Task.Run<IReadOnlyList<ParsedStatementTransaction>>(() =>
        {
            using var document = PdfDocument.Open(filePath);
            var extractedPages = document.GetPages()
                .Select(page => ContentOrderTextExtractor.GetText(page))
                .ToList();
            var extractedText = string.Join(Environment.NewLine, extractedPages);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidDataException(
                    "This PDF has no selectable text. Export a text-based PDF or CSV; scanned statements require OCR.");
            }

            var transactions = ParseExtractedText(extractedText, amountConvention);
            if (transactions.Count == 0)
            {
                throw new InvalidDataException(
                    "Text was read from the PDF, but no transaction rows were recognized. Use CSV for this statement layout.");
            }

            return transactions;
        });
    }

    public static IReadOnlyList<ParsedStatementTransaction> ParseExtractedText(
        string extractedText,
        StatementAmountConvention amountConvention)
    {
        var parsedRows = new List<(DateTime Date, string Description, decimal SignedAmount)>();
        var lines = extractedText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = string.Join(' ', rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var dateMatch = TransactionDateRegex().Match(line);
            if (!dateMatch.Success || !TryParseDate(dateMatch.Groups["date"].Value, out var date))
                continue;

            var amountMatches = TransactionAmountRegex().Matches(line[dateMatch.Length..]);
            if (amountMatches.Count == 0) continue;

            var amountMatch = amountMatches[0];
            if (!TryParseAmount(amountMatch.Groups["amount"].Value, out var signedAmount)
                || signedAmount == 0)
                continue;

            var descriptionStart = dateMatch.Length;
            var descriptionLength = amountMatch.Index;
            var description = line.Substring(descriptionStart, descriptionLength).Trim(' ', '-', '|');
            if (string.IsNullOrWhiteSpace(description))
                description = "PDF statement transaction";

            parsedRows.Add((date, description, signedAmount));
        }

        var detectedConvention = StatementTransactionClassifier.DetectAmountConvention(
            parsedRows.Select(row => (
                row.SignedAmount,
                StatementTransactionClassifier.ClassifyExplicit(row.Description))),
            amountConvention);

        return parsedRows.Select(row => new ParsedStatementTransaction
            {
                Date = row.Date,
                Description = row.Description,
                Amount = Math.Abs(row.SignedAmount),
                Type = StatementTransactionClassifier.ClassifySignedAmount(
                    row.Description,
                    row.SignedAmount,
                    detectedConvention)
            })
            .ToList();
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy",
            "MMM d yyyy", "MMM dd yyyy", "MMMM d yyyy", "MMMM dd yyyy"
        };
        var normalized = value.Replace(",", string.Empty).Trim();
        if (DateTime.TryParseExact(
                normalized,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
            return true;

        var formatsWithoutYear = new[] { "MMM d", "MMM dd", "MMMM d", "MMMM dd", "MM/dd", "M/d" };
        if (DateTime.TryParseExact(
                normalized,
                formatsWithoutYear,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var partialDate))
        {
            date = new DateTime(DateTime.Today.Year, partialDate.Month, partialDate.Day);
            return true;
        }

        return DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Trim();
        var isNegativeParentheses = normalized.StartsWith('(') && normalized.EndsWith(')');
        normalized = normalized
            .Trim('(', ')')
            .Replace("CAD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty)
            .Replace(" ", string.Empty);

        var parsed = decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
        if (parsed && isNegativeParentheses) amount = -Math.Abs(amount);
        return parsed;
    }

    [GeneratedRegex(
        @"^\s*(?<date>(?:\d{4}[-/]\d{1,2}[-/]\d{1,2})|(?:\d{1,2}[-/]\d{1,2}(?:[-/]\d{2,4})?)|(?:[A-Za-z]{3,9}\s+\d{1,2}(?:,?\s+\d{4})?))\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex TransactionDateRegex();

    [GeneratedRegex(
        @"(?<![\d.])(?<amount>\(?\s*(?:CAD\s*)?\$?\s*-?(?:\d{1,3}(?:,\d{3})+|\d+)\.\d{2}\s*\)?)(?![\d.])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransactionAmountRegex();
}
