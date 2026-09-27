using FuncPulse.Core.Models;
using System.Xml;

namespace FuncPulse.Tests;

public class TimeRangeTests
{
    [Theory]
    [InlineData(TimeRange.Hours24, "PT1H")]
    [InlineData(TimeRange.Days7, "P1D")]
    [InlineData(TimeRange.Days30, "P1D")]
    public void ToMetricInterval_ReturnsValidISO8601Duration(TimeRange range, string expectedInterval)
    {
        // Arrange & Act
        var interval = range.ToMetricInterval();
        
        // Assert
        Assert.Equal(expectedInterval, interval);
        
        // Verify it can be parsed as ISO-8601 duration
        var parsed = XmlConvert.ToTimeSpan(interval);
        Assert.True(parsed.TotalSeconds > 0, $"ISO-8601 duration '{interval}' should parse to positive TimeSpan");
    }

    [Fact]
    public void ToMetricInterval_AllValues_CanBeParsedByXmlConvert()
    {
        // Arrange
        var allRanges = Enum.GetValues<TimeRange>();
        
        // Act & Assert
        foreach (var range in allRanges)
        {
            var interval = range.ToMetricInterval();
            
            // This should not throw FormatException
            var parsed = XmlConvert.ToTimeSpan(interval);
            
            Assert.True(parsed.TotalSeconds > 0, 
                $"TimeRange.{range} returns '{interval}' which should be a valid ISO-8601 duration");
        }
    }

    [Fact]
    public void ToMetricInterval_PT1H_ParsesCorrectly()
    {
        // This is the regression test for the PT1H FormatException bug
        // TimeSpan.Parse("PT1H") throws FormatException
        // XmlConvert.ToTimeSpan("PT1H") works correctly
        
        var iso8601Duration = "PT1H";
        
        // Verify TimeSpan.Parse fails (documenting the bug)
        Assert.Throws<FormatException>(() => TimeSpan.Parse(iso8601Duration));
        
        // Verify XmlConvert.ToTimeSpan works (the fix)
        var parsed = XmlConvert.ToTimeSpan(iso8601Duration);
        Assert.Equal(TimeSpan.FromHours(1), parsed);
    }

    [Fact]
    public void ToMetricInterval_P1D_ParsesCorrectly()
    {
        var iso8601Duration = "P1D";
        
        // Verify TimeSpan.Parse fails
        Assert.Throws<FormatException>(() => TimeSpan.Parse(iso8601Duration));
        
        // Verify XmlConvert.ToTimeSpan works
        var parsed = XmlConvert.ToTimeSpan(iso8601Duration);
        Assert.Equal(TimeSpan.FromDays(1), parsed);
    }
}
