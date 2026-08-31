using System.Text;

namespace ExpenseTracker.Services;

public static class StatementFileValidator
{
    public const long MaximumFileSize = 10 * 1024 * 1024;
    private const int HeaderSize = 4096;

    public static async Task CopyAndValidateAsync(
        Stream source,
        string destinationPath,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (source.CanSeek && source.Length - source.Position > MaximumFileSize)
            throw new InvalidDataException("Statement files cannot exceed 10 MB.");

        var buffer = new byte[81920];
        long totalBytes = 0;
        await using (var destination = new FileStream(
                         destinationPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         buffer.Length,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            while (true)
            {
                var bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                totalBytes += bytesRead;
                if (totalBytes > MaximumFileSize)
                    throw new InvalidDataException("Statement files cannot exceed 10 MB.");

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }

        if (totalBytes == 0)
            throw new InvalidDataException("The selected statement file is empty.");

        await ValidateSignatureAsync(destinationPath, extension, cancellationToken);
    }

    private static async Task ValidateSignatureAsync(
        string filePath,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        await using var stream = File.OpenRead(filePath);
        var bytesRead = await stream.ReadAsync(header, cancellationToken);
        var content = header.AsSpan(0, bytesRead);

        if (extension == ".pdf")
        {
            if (content.Length < 5 || !content[..5].SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("The selected file is not a valid PDF statement.");
            return;
        }

        if (extension != ".csv")
            throw new InvalidDataException("Only CSV and PDF statements are supported.");
        if (content.Contains((byte)0))
            throw new InvalidDataException("The selected CSV contains binary data.");

        var text = Encoding.UTF8.GetString(content).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!text.Contains(',') && !text.Contains(';') && !text.Contains('\t'))
            throw new InvalidDataException("The selected file does not contain a recognizable CSV header.");
    }
}
