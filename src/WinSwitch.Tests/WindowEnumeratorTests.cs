using WinSwitch.Core.Models;
using WinSwitch.Core.Services;
using Xunit;

namespace WinSwitch.Tests;

public class WindowEnumeratorTests
{
    [Theory]
    [InlineData("客服工作台", "客服工作台", TitleMatchType.Contains, true)]
    [InlineData("客服工作台 - Chrome", "客服工作台", TitleMatchType.Contains, true)]
    [InlineData("广告后台管理", "客服工作台", TitleMatchType.Contains, false)]
    [InlineData("广告后台管理", "广告", TitleMatchType.StartsWith, true)]
    [InlineData("广告后台管理", "广告后台管理", TitleMatchType.Exact, true)]
    [InlineData("广告后台管理-v2", "广告后台管理", TitleMatchType.Exact, false)]
    [InlineData("", "anything", TitleMatchType.Contains, true)]  // 空模式匹配所有
    public void IsTitleMatch_ShouldMatchCorrectly(string title, string pattern, TitleMatchType matchType, bool expected)
    {
        var result = WindowEnumerator.IsTitleMatch(title, pattern, matchType);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsTitleMatch_WithRegex_ShouldMatch()
    {
        var result = WindowEnumerator.IsTitleMatch("客服工作台 #123", @"#\d+", TitleMatchType.Regex);

        Assert.True(result);
    }

    [Fact]
    public void IsTitleMatch_WithRegexNoMatch_ShouldReturnFalse()
    {
        var result = WindowEnumerator.IsTitleMatch("客服工作台", @"#\d+", TitleMatchType.Regex);

        Assert.False(result);
    }
}
