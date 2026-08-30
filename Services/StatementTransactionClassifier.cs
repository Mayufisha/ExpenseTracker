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
        return ClassifyExplicit(description) ?? fallback;
    }

    public static TransactionType? ClassifyExplicit(string description, string direction = "")
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

        return ClassifyDirection(direction);
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
        return ClassifyExplicit(description, direction) ?? fallback;
    }

    public static StatementAmountConvention DetectAmountConvention(
        IEnumerable<(decimal SignedAmount, TransactionType? ExplicitType)> evidence,
        StatementAmountConvention fallback)
    {
        var negativeExpenseVotes = 0;
        var positiveExpenseVotes = 0;

        foreach (var (signedAmount, explicitType) in evidence)
        {
            if (signedAmount == 0 || explicitType == null) continue;

            var positiveMeansExpense = (signedAmount > 0) == (explicitType == TransactionType.Expense);
            if (positiveMeansExpense)
                positiveExpenseVotes++;
            else
                negativeExpenseVotes++;
        }

        if (negativeExpenseVotes >= 2 && negativeExpenseVotes > positiveExpenseVotes * 2)
            return StatementAmountConvention.NegativeAmountsAreExpenses;
        if (positiveExpenseVotes >= 2 && positiveExpenseVotes > negativeExpenseVotes * 2)
            return StatementAmountConvention.PositiveAmountsAreExpenses;
        return fallback;
    }

    private static TransactionType? ClassifyDirection(string direction)
    {
        var normalized = Normalize(direction);
        if (new[] { " debit ", " withdrawal ", " charge ", " purchase ", " sent ", " outgoing " }
            .Any(normalized.Contains))
            return TransactionType.Expense;
        if (new[] { " credit ", " deposit ", " received ", " incoming " }
            .Any(normalized.Contains))
            return TransactionType.Income;
        return null;
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
