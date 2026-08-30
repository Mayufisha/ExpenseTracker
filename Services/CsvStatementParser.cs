using System.Globalization;
using System.Text;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public static class CsvStatementParser
{
    private static readonly string[] DateHeaders = ["date", "transactiondate", "posteddate", "postingdate"];
    private static readonly string[] DescriptionHeaders = ["description", "memo", "details", "name", "transaction"];
    private static readonly string[] AmountHeaders = ["amount", "transactionamount"];
    private static readonly string[] DebitHeaders = ["debit", "withdrawal", "charge"];
    private static readonly string[] CreditHeaders = ["credit", "deposit", "payment"];
    private static readonly string[] DirectionHeaders = ["type", "transactiontype", "debitorcredit", "direction"];

    public static async Task<IReadOnlyList<ParsedStatementTransaction>> ParseAsync(
        string filePath,
        string accountType)
    {
        var convention = accountType.Contains("credit", StringComparison.OrdinalIgnoreCase)
            ? StatementAmountConvention.PositiveAmountsAreExpenses
            : StatementAmountConvention.NegativeAmountsAreExpenses;
        return await ParseAsync(filePath, convention);
    }

    public static async Task<IReadOnlyList<ParsedStatementTransaction>> ParseAsync(
        string filePath,
        StatementAmountConvention amountConvention)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2)
            throw new InvalidDataException("The CSV statement does not contain transaction rows.");

        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter)
            .Select(NormalizeHeader)
            .ToList();

        var dateIndex = FindHeader(headers, DateHeaders);
        var descriptionIndex = FindHeader(headers, DescriptionHeaders);
        var amountIndex = FindHeader(headers, AmountHeaders);
        var debitIndex = FindHeader(headers, DebitHeaders);
        var creditIndex = FindHeader(headers, CreditHeaders);
        var directionIndex = FindHeader(headers, DirectionHeaders);

        if (dateIndex < 0 || (amountIndex < 0 && debitIndex < 0 && creditIndex < 0))
        {
            throw new InvalidDataException(
                "CSV must include a date column and either amount, debit, or credit columns.");
        }

        var rows = lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => (IReadOnlyList<string>)ParseLine(line, delimiter))
            .ToList();
        var detectedConvention = DetectAmountConvention(
            rows,
            amountIndex,
            descriptionIndex,
            directionIndex,
            amountConvention);
        var result = new List<ParsedStatementTransaction>();

        foreach (var cells in rows)
        {
            if (!TryGet(cells, dateIndex, out var dateText) || !TryParseDate(dateText, out var date))
                continue;

            var description = TryGet(cells, descriptionIndex, out var descriptionText)
                ? descriptionText.Trim()
                : "Statement transaction";

            if (!TryResolveAmount(
                    cells,
                    amountIndex,
                    debitIndex,
                    creditIndex,
                    directionIndex,
                    description,
                    detectedConvention,
                    out var amount,
                    out var type))
                continue;

            result.Add(new ParsedStatementTransaction
            {
                Date = date,
                Description = description,
                Amount = amount,
                Type = type
            });
        }

        return result;
    }

    private static StatementAmountConvention DetectAmountConvention(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int amountIndex,
        int descriptionIndex,
        int directionIndex,
        StatementAmountConvention fallback)
    {
        if (amountIndex < 0) return fallback;

        var evidence = rows.Select(cells =>
        {
            var signedAmount = 0m;
            var hasAmount = TryGet(cells, amountIndex, out var amountText)
                && TryParseAmount(amountText, out signedAmount);
            var description = TryGet(cells, descriptionIndex, out var descriptionText)
                ? descriptionText
                : string.Empty;
            var direction = TryGet(cells, directionIndex, out var directionText)
                ? directionText
                : string.Empty;
            return (
                SignedAmount: hasAmount ? signedAmount : 0,
                ExplicitType: StatementTransactionClassifier.ClassifyExplicit(description, direction));
        });

        return StatementTransactionClassifier.DetectAmountConvention(evidence, fallback);
    }

    private static bool TryResolveAmount(
        IReadOnlyList<string> cells,
        int amountIndex,
        int debitIndex,
        int creditIndex,
        int directionIndex,
        string description,
        StatementAmountConvention amountConvention,
        out decimal amount,
        out TransactionType type)
    {
        amount = 0;
        type = TransactionType.Expense;

        if (TryGet(cells, debitIndex, out var debitText) && TryParseAmount(debitText, out var debit) && debit != 0)
        {
            amount = Math.Abs(debit);
            type = StatementTransactionClassifier.Classify(description, TransactionType.Expense);
            return true;
        }

        if (TryGet(cells, creditIndex, out var creditText) && TryParseAmount(creditText, out var credit) && credit != 0)
        {
            amount = Math.Abs(credit);
            type = StatementTransactionClassifier.Classify(description, TransactionType.Income);
            return true;
        }

        if (!TryGet(cells, amountIndex, out var amountText) || !TryParseAmount(amountText, out var signedAmount) || signedAmount == 0)
            return false;

        amount = Math.Abs(signedAmount);
        var direction = TryGet(cells, directionIndex, out var directionText)
            ? directionText
            : string.Empty;
        type = StatementTransactionClassifier.ClassifySignedAmount(
            description,
            signedAmount,
            amountConvention,
            direction);
        return true;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Trim();
        var isNegativeParentheses = normalized.StartsWith('(') && normalized.EndsWith(')');
        normalized = normalized.Trim('(', ')');

        var parsed = decimal.TryParse(normalized, NumberStyles.Currency | NumberStyles.AllowLeadingSign,
                CultureInfo.CurrentCulture, out amount)
            || decimal.TryParse(normalized, NumberStyles.Currency | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out amount);

        if (parsed && isNegativeParentheses)
            amount = -Math.Abs(amount);

        return parsed;
    }

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { ',', ';', '\t' };
        return candidates.OrderByDescending(c => header.Count(ch => ch == c)).First();
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static string NormalizeHeader(string header)
    {
        return new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static int FindHeader(IReadOnlyList<string> headers, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            for (var index = 0; index < headers.Count; index++)
            {
                if (headers[index] == candidate) return index;
            }
        }

        return -1;
    }

    private static bool TryGet(IReadOnlyList<string> values, int index, out string value)
    {
        if (index >= 0 && index < values.Count)
        {
            value = values[index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
