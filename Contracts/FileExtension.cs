// -----------------------------------------------------------------------
// <copyright file="FileExtension.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    public static class FileExtension
    {
        /// <summary>
        /// Asynchronously reads all text from a file.
        /// </summary>
        /// <param name="filePath">The path to the file to read from.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation that returns the file content as a string.</returns>
        public static async Task<string> ReadAllTextAsync(this string filePath, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}", filePath);
            }

            // Use StreamReader for text-based reading with UTF-8 encoding
            using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);

            // Read all text content asynchronously
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously writes text to a file, creating the file if it doesn't exist or overwriting it if it does.
        /// </summary>
        /// <param name="filePath">The path to the file to write to.</param>
        /// <param name="contents">The string content to write to the file.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task WriteAllTextAsync(this string filePath, string contents, CancellationToken token = default)
        {
            if (contents == null)
            {
                contents = string.Empty;
            }

            // Use StreamWriter for text-based writing with UTF-8 encoding
            using var fs = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8, bufferSize: 4096, leaveOpen: false);

            // Write text directly without manual byte conversion
            await writer.WriteAsync(contents).ConfigureAwait(false);

            // No FlushAsync for performance reasons - let OS schedule the actual disk write
        }

        public static async Task<string> GetFileHashAsync(this string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filePath);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var fileHashBytes = sha256.Hash;
            var actualFileHashString = BitConverter
                .ToString(fileHashBytes)
                .Replace("-", string.Empty);
            return actualFileHashString;
        }
    }
}
