﻿using System.IO;
using System.Threading.Tasks;
using Verify;

static class Comparer
{
    public static async Task<EqualityResult> Text(FilePair file, string scrubbedInput, VerifySettings settings)
    {
        scrubbedInput = Scrub(scrubbedInput, settings.ignoreTrailingWhitespace);
        FileHelpers.DeleteIfEmpty(file.Verified);
        if (!File.Exists(file.Verified))
        {
            await FileHelpers.WriteText(file.Received, scrubbedInput);
            return Equality.MissingVerified;
        }

        var verifiedText = await FileHelpers.ReadText(file.Verified);
        verifiedText = Scrub(verifiedText, settings.ignoreTrailingWhitespace);

        var result = await CompareStrings(scrubbedInput, verifiedText, settings);
        if (result.IsEqual)
        {
            return Equality.Equal;
        }

        await FileHelpers.WriteText(file.Received, scrubbedInput);
        return new EqualityResult(Equality.NotEqual, result.Message);
    }

    static async Task<CompareResult> CompareStrings(string scrubbedInput, string verifiedText, VerifySettings settings)
    {
        var extension = settings.ExtensionOrTxt();
        if (settings.comparer != null)
        {
            using var stream1 = MemoryStream(scrubbedInput);
            using var stream2 = MemoryStream(verifiedText);
            return await settings.comparer(settings, stream1, stream2);
        }
        if (SharedVerifySettings.TryGetComparer(extension, out var comparer))
        {
            using var stream1 = MemoryStream(scrubbedInput);
            using var stream2 = MemoryStream(verifiedText);
            return await comparer(settings, stream1, stream2);
        }

        var result = new CompareResult(string.Equals(verifiedText, scrubbedInput));
        return result;
    }

    static MemoryStream MemoryStream(string text)
    {
        return new MemoryStream(FileHelpers.Utf8NoBOM.GetBytes(text));
    }

    static string Scrub(string scrubbedInput, bool ignoreTrailingWhitespace)
    {
        scrubbedInput = scrubbedInput.Replace("\r\n", "\n");
        if (ignoreTrailingWhitespace)
        {
            scrubbedInput = scrubbedInput.TrimEnd();
        }

        return scrubbedInput;
    }

    public static async Task<EqualityResult> Streams(
        VerifySettings settings,
        Stream stream,
        FilePair file)
    {
        try
        {
            await FileHelpers.WriteStream(file.Received, stream);

            var result = await FileComparer.DoCompare(settings, file);

            if (result.Equality == Equality.Equal)
            {
                File.Delete(file.Received);
                return result;
            }

            return result;
        }
        finally
        {
#if NETSTANDARD2_1
            await stream.DisposeAsync();
#else
            stream.Dispose();
#endif
        }
    }
}