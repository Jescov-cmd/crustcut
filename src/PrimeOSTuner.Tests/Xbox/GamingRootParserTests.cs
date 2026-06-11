using System;
using System.Text;
using FluentAssertions;
using PrimeOSTuner.Win.Xbox;
using Xunit;

namespace PrimeOSTuner.Tests.Xbox;

public class GamingRootParserTests
{
    private static byte[] Bytes(string path)
    {
        var bytes = new System.Collections.Generic.List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes("RGBX"));
        bytes.AddRange(new byte[] { 1, 0, 0, 0 });            // folder count (observed layout)
        bytes.AddRange(Encoding.Unicode.GetBytes(path));
        bytes.AddRange(new byte[] { 0, 0 });                  // UTF-16 null terminator
        return bytes.ToArray();
    }

    [Fact]
    public void Parses_folder_name_from_valid_file()
        => XboxLibraryScanner.ParseGamingRoot(Bytes("MyGames"), 'D')
            .Should().Be(@"D:\MyGames");

    [Fact]
    public void Wrong_magic_returns_null()
        => XboxLibraryScanner.ParseGamingRoot(Encoding.ASCII.GetBytes("NOPE1234"), 'D')
            .Should().BeNull();

    [Fact]
    public void Truncated_or_empty_returns_null()
    {
        XboxLibraryScanner.ParseGamingRoot(Array.Empty<byte>(), 'D').Should().BeNull();
        XboxLibraryScanner.ParseGamingRoot(Encoding.ASCII.GetBytes("RGBX"), 'D').Should().BeNull();
    }
}
