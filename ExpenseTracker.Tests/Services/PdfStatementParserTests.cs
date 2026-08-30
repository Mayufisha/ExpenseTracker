using ExpenseTracker.Models;
using ExpenseTracker.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ExpenseTracker.Tests.Services;

public class PdfStatementParserTests
{
    [Fact]
    public void ParseExtractedText_ClassifiesTransfersAndPayrollFromDescriptions()
    {
        const string text =
            "Date Description Amount Balance\n" +
            "2026-08-01 INTERAC E-TRANSFER SENT TO ALEX 75.00 925.00\n" +
            "2026-08-02 PAYROLL DIRECT DEPOSIT -1000.00 1925.00";

        var result = PdfStatementParser.ParseExtractedText(
            text,
            StatementAmountConvention.NegativeAmountsAreExpenses);

        Assert.Equal(2, result.Count);
        Assert.Equal(TransactionType.Expense, result[0].Type);
        Assert.Equal(75m, result[0].Amount);
        Assert.Equal(TransactionType.Income, result[1].Type);
    }

    [Fact]
    public async Task ParseAsync_ReadsTransactionsFromTextBasedPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"statement-{Guid.NewGuid():N}.pdf");
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.Letter);
        page.AddText(
            "2026-08-01 E-TRANSFER SENT TO ALEX 75.00 925.00",
            10,
            new PdfPoint(40, 700),
            font);
        await File.WriteAllBytesAsync(path, builder.Build());

        try
        {
            var result = await PdfStatementParser.ParseAsync(
                path,
                StatementAmountConvention.NegativeAmountsAreExpenses);

            var transaction = Assert.Single(result);
            Assert.Equal(TransactionType.Expense, transaction.Type);
            Assert.Equal(75m, transaction.Amount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
