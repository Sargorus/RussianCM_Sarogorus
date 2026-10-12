using Content.Shared.CMU14.Callsigns;
using NUnit.Framework;

namespace Content.Tests.CMU14;

[TestFixture]
public sealed class CMUCallsignNumberTest
{
    [TestCase("1", "01")]
    [TestCase(" 0001 ", "01")]
    [TestCase("99", "99")]
    [TestCase("100", "100")]
    public void NormalizesStationNumber(string input, string expected)
    {
        Assert.That(AU14Callsigns.TryNormalizeNumber(input, out var number), Is.True);
        Assert.That(number, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("000")]
    [TestCase("ROMEO")]
    [TestCase("1-1")]
    [TestCase("1 2")]
    [TestCase("-1")]
    [TestCase("١٢")]
    [TestCase("123456789")]
    public void RejectsNonStationNumbers(string input)
    {
        Assert.That(AU14Callsigns.TryNormalizeNumber(input, out _), Is.False);
    }
}
