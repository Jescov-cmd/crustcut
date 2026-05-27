using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameTimeParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "presentmon", name);

    [Fact]
    public void Parse_valid_csv_returns_five_samples()
    {
        var path = FixturePath("valid-short.csv");

        var samples = FrameTimeParser.ParseFile(path);

        samples.Should().HaveCount(5);
        samples[0].Should().BeApproximately(0.0, 0.001);
        samples[3].Should().BeApproximately(66.7, 0.001);
    }

    [Fact]
    public void Parse_empty_file_returns_an_empty_list()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("empty.csv"));

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_header_only_csv_returns_an_empty_list()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("header-only.csv"));

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_missing_file_returns_an_empty_list_without_throwing()
    {
        var samples = FrameTimeParser.ParseFile(@"C:\does\not\exist.csv");

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_preserves_zero_rows_so_the_stats_calculator_can_filter_them()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("with-zero-rows.csv"));

        samples.Should().HaveCount(3);
        samples[0].Should().Be(0.0);    // raw value, stats calculator filters
        samples[1].Should().BeApproximately(16.7, 0.001);
        samples[2].Should().Be(0.0);
    }

    [Fact]
    public void Parse_csv_without_msBetweenPresents_column_returns_empty()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "Application,ProcessID\ngame.exe,1234\n");

            var samples = FrameTimeParser.ParseFile(tmp);

            samples.Should().BeEmpty();
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
