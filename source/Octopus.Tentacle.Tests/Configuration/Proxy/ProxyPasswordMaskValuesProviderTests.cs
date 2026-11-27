using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Proxy;

namespace Octopus.Tentacle.Tests.Configuration.Proxy
{
    public class ProxyPasswordMaskValuesProviderTests
    {
        [TestCase("")]
        [TestCase(null)]
        public void GetProxyPasswordMaskValues_NullOrEmpty_EmptyList(string password)
        {
            var sut = new ProxyPasswordMaskValuesProvider();
            sut.GetProxyPasswordMaskValues(password).Should().BeEmpty();
        }
        
        [Test]
        public void GetProxyPasswordMaskValues_NoSpecialCharacters_JustPassword()
        {
            var sut = new ProxyPasswordMaskValuesProvider();
            string password = "Some-sTAr";
            sut.GetProxyPasswordMaskValues(password).Should().BeEquivalentTo(new []
            {
                "Some-sTAr"
            });
        }
        
        [Test]
        public void GetProxyPasswordMaskValues_HasSpecialCharacters_EncodedWithBothCases()
        {
            var sut = new ProxyPasswordMaskValuesProvider();
            string password = "Some@sT:r/";
            sut.GetProxyPasswordMaskValues(password).Should().BeEquivalentTo(new []
            {
                "Some@sT:r/",
                "Some%40sT%3Ar%2F", //WebUtility.UrlEncode generates uppercase hex characters
                "Some%40sT%3ar%2f"  //HttpUtility.UrlEncode generates lowercase hex characters
            });
        }
        
        [Test]
        public void GetProxyPasswordMaskValues_HasSpecialCharactersWithHexCharactersFollowing_HexCharacterCasingPreserved()
        {
            var sut = new ProxyPasswordMaskValuesProvider();
            string password = "Some@sT:Ar/";
            sut.GetProxyPasswordMaskValues(password).Should().BeEquivalentTo(new []
            {
                "Some@sT:Ar/",
                "Some%40sT%3AAr%2F", //WebUtility.UrlEncode generates uppercase hex characters
                "Some%40sT%3aAr%2f"  //HttpUtility.UrlEncode generates lowercase hex characters
            });
        }
    }
}