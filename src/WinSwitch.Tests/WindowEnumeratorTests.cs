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

    // ===== 包含匹配模式：关键词必须完全匹配（非子串包含） =====

    [Fact]
    public void IsFullKeywordMatch_ChineseKeyword_ShouldMatchSubstring()
    {
        // 中文无词边界概念，直接包含即可
        var result = WindowEnumerator.IsFullKeywordMatch("客服工作台 - Chrome", "客服工作台");
        Assert.True(result);
    }

    [Fact]
    public void IsFullKeywordMatch_ChineseKeyword_NoMatch()
    {
        var result = WindowEnumerator.IsFullKeywordMatch("广告后台管理", "客服工作台");
        Assert.False(result);
    }

    [Fact]
    public void IsFullKeywordMatch_EnglishKeyword_WordBoundaryMatch()
    {
        // 英文关键词按词边界匹配，"Chrome" 是独立词
        var result = WindowEnumerator.IsFullKeywordMatch("客服工作台 - Chrome", "Chrome");
        Assert.True(result);
    }

    [Fact]
    public void IsFullKeywordMatch_EnglishKeyword_SubstringNoMatch()
    {
        // "Chr" 是 "Chrome" 的子串，不应匹配
        var result = WindowEnumerator.IsFullKeywordMatch("客服工作台 - Chrome", "Chr");
        Assert.False(result);
    }

    [Fact]
    public void IsFullKeywordMatch_EnglishKeyword_PartialWordNoMatch()
    {
        // "Edge" 不在 "EdgeCase" 中作为独立词
        var result = WindowEnumerator.IsFullKeywordMatch("EdgeCase Browser", "Edge");
        Assert.False(result);
    }

    [Fact]
    public void IsFullKeywordMatch_EmptyInputs_ShouldReturnFalse()
    {
        Assert.False(WindowEnumerator.IsFullKeywordMatch("", "test"));
        Assert.False(WindowEnumerator.IsFullKeywordMatch("test", ""));
        Assert.False(WindowEnumerator.IsFullKeywordMatch("", ""));
    }

    // ===== 包含匹配模式改造：IsTitleMatch 的 Contains 模式现在用词边界 =====

    [Fact]
    public void IsTitleMatch_Contains_EnglishFullWord()
    {
        // "Chrome" 是独立词，应匹配
        var result = WindowEnumerator.IsTitleMatch("My Chrome Window", "Chrome", TitleMatchType.Contains);
        Assert.True(result);
    }

    [Fact]
    public void IsTitleMatch_Contains_EnglishSubstring_NoMatch()
    {
        // "Chr" 不是独立词，不应匹配
        var result = WindowEnumerator.IsTitleMatch("My Chrome Window", "Chr", TitleMatchType.Contains);
        Assert.False(result);
    }

    [Fact]
    public void IsTitleMatch_Contains_ChineseStillWorks()
    {
        // 中文包含匹配仍然有效
        var result = WindowEnumerator.IsTitleMatch("客服工作台 - Chrome", "客服", TitleMatchType.Contains);
        Assert.True(result);
    }

    [Fact]
    public void IsTitleMatch_Contains_MultipleKeywords_OneMustMatch()
    {
        // 分号分隔多关键词，任一完整匹配即返回 true
        var result = WindowEnumerator.IsTitleMatch("My Edge Browser", "Chrome;Edge;Firefox", TitleMatchType.Contains);
        Assert.True(result);
    }

    [Fact]
    public void IsTitleMatch_Contains_MultipleKeywords_NoneMatch()
    {
        var result = WindowEnumerator.IsTitleMatch("My Safari Browser", "Chrome;Edge;Firefox", TitleMatchType.Contains);
        Assert.False(result);
    }

    [Fact]
    public void IsTitleMatch_Contains_MultipleKeywords_SubstringNoMatch()
    {
        // "Edg" 不是独立词，即使分号分隔也不应匹配
        var result = WindowEnumerator.IsTitleMatch("My Edge Browser", "Chr;Edg;Fir", TitleMatchType.Contains);
        Assert.False(result);
    }
}