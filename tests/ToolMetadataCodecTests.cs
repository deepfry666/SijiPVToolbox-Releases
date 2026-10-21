using System;
using StructuralToolbox.Core;

internal static class ToolMetadataCodecTests
{
    private static int _passed;

    private static void Main()
    {
        AcceptsExactly255AsciiBytes();
        Rejects256AsciiBytesWithoutTruncating();
        CountsUtf8BytesInsteadOfCharacters();
        Console.WriteLine("PASS {0} tool metadata codec tests", _passed);
    }

    private static void AcceptsExactly255AsciiBytes()
    {
        string value = new string('A', 255);
        Equal(value, ToolMetadataCodec.RequireXDataString(value, "TEST"),
            "255-byte value");
        Pass();
    }

    private static void Rejects256AsciiBytesWithoutTruncating()
    {
        InvalidOperationException exception = Throws<InvalidOperationException>(
            delegate
            {
                ToolMetadataCodec.RequireXDataString(
                    new string('B', 256), "LONG_FIELD");
            }, "256-byte value");
        Contains(exception.Message, "LONG_FIELD", "field name in error");
        Contains(exception.Message, "256", "actual bytes in error");
        Contains(exception.Message, "255", "limit in error");
        Pass();
    }

    private static void CountsUtf8BytesInsteadOfCharacters()
    {
        string exactly255Bytes = new string('\u5bfc', 85);
        Equal(exactly255Bytes, ToolMetadataCodec.RequireXDataString(
            exactly255Bytes, "UTF8"), "85 Chinese characters");
        InvalidOperationException exception = Throws<InvalidOperationException>(
            delegate
            {
                ToolMetadataCodec.RequireXDataString(
                    new string('\u5bfc', 86), "UTF8");
            }, "258-byte UTF-8 value");
        Contains(exception.Message, "258", "UTF-8 byte count");
        Pass();
    }

    private static TException Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        throw new InvalidOperationException(message + " did not throw " +
            typeof(TException).Name);
    }

    private static void Contains(string actual, string expected, string message)
    {
        if (actual == null || actual.IndexOf(expected,
            StringComparison.Ordinal) < 0)
        {
            throw new InvalidOperationException(message + ": expected " +
                expected + " in " + actual);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!Object.Equals(expected, actual))
        {
            throw new InvalidOperationException(message + ": expected " +
                expected + ", actual " + actual);
        }
    }

    private static void Pass()
    {
        _passed++;
    }
}
