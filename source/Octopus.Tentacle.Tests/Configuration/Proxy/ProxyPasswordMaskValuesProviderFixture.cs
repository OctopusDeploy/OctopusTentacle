using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Proxy;

namespace Octopus.Tentacle.Tests.Configuration.Proxy
{
    public class ProxyPasswordMaskValuesProviderFixture
    {
        ProxyPasswordMaskValuesProvider sut;

        [SetUp]
        public void SetUp()
        {
            sut = new ProxyPasswordMaskValuesProvider();
        }

        [TestCase("")]
        [TestCase(null)]
        public void GetProxyPasswordMaskValues_NullOrEmpty_EmptyList(string password)
        {
            sut.GetProxyPasswordMaskValues(password).Should().BeEmpty();
        }
        
        [Test]
        public void GetProxyPasswordMaskValues_NoSpecialCharacters_JustPassword()
        {
            string password = "Some-sTAr";
            sut.GetProxyPasswordMaskValues(password).Should().BeEquivalentTo(new []
            {
                "Some-sTAr"
            });
        }
        
        [Test]
        public void GetProxyPasswordMaskValues_HasSpecialCharacters_EncodedWithBothCases()
        {
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