using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

public static class StatementTransactionClassifier
{
    private static readonly string[] TransferTerms =
    [
        "e transfer",
        "etransfer",
        "e trf",
        "etrf",
        "interac transfer",
        "interac e trf",
        "email transfer",
        "email money transfer"
    ];

    private static readonly string[] IncomingTransferTerms =
    [
        " received ",
        " receive ",
        " from ",
        " incoming ",
        " auto deposit ",
        " autodeposit "
    ];

    private static readonly string[] IncomeTerms =
    [
        " payroll ",
        " salary ",
        " direct deposit ",
        " government deposit ",
        " interest paid ",
        " interest credit ",
        " refund ",
        " reimbursement "
    ];

    private static readonly string[] ExpenseTerms =
    [
        " purchase ",
        " point of sale ",
        " pos ",
        " bill payment ",
        " pre authorized debit ",
        " preauthorized debit ",
        " withdrawal ",
        " service charge ",
        " monthly fee ",
        " debit card "
    ];

    public static TransactionType Classify(string description, TransactionType fallback)
    {
        var normalized = Normalize(description);
        var isTransfer = TransferTerms.Any(normalized.Contains);

        if (isTransfer && IncomingTransferTerms.Any(normalized.Contains))
            return TransactionType.Income;
        if (isTransfer)
            return TransactionType.Expense;
        if (IncomeTerms.Any(normalized.Contains))
            return TransactionType.Income;
        if (ExpenseTerms.Any(normalized.Contains))
            return TransactionType.Expense;

        return fallback;
    }

    public static TransactionType ClassifySignedAmount(
        string description,
        decimal signedAmount,
        StatementAmountConvention amountConvention,
        string direction = "")
    {
        var positiveAmountsAreExpenses = amountConvention == StatementAmountConvention.PositiveAmountsAreExpenses;
        var fallback = positiveAmountsAreExpenses == (signedAmount > 0)
            ? TransactionType.Expense
            : TransactionType.Income;
        return Classify(description, ClassifyDirection(direction, fallback));
    }

    private static TransactionType ClassifyDirection(string direction, TransactionType fallback)
    {
        var normalized = Normalize(direction);
        if (new[] { " debit ", " withdrawal ", " charge ", " purchase ", " sent ", " outgoing " }
            .Any(normalized.Contains))
            return TransactionType.Expense;
        if (new[] { " credit ", " deposit ", " received ", " incoming " }
            .Any(normalized.Contains))
            return TransactionType.Income;
        return fallback;
    }

    private static string Normalize(string value)
    {
        var characters = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return $" {string.Join(' ', new string(characters).Split(' ', StringSplitOptions.RemoveEmptyEntries))} ";
    }
}
