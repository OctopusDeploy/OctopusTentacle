using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Web;

namespace Octopus.Shared.Tests.Web
{
    public class WebRoutesFixture
    {

        public IEnumerable<TestCaseData> ApiRoutes()
        {
            return GetRoutes(typeof(WebRoutes.Api)).ToArray();
        }

        public IEnumerable<TestCaseData> WebRoutes()
        {
            return GetRoutes(typeof(WebRoutes.Web)).ToArray();
        }


        private IEnumerable<TestCaseData> GetRoutes(Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => CreateCase(f, (string) f.GetValue(null)));

            var nested = type.GetNestedTypes().SelectMany(GetRoutes);

            return fields.Concat(nested);
        }

        private TestCaseData CreateCase(MemberInfo member, string value)
        {
            var data = new TestCaseData(value);
            data.SetName(member.DeclaringType?.FullName + "." + member.Name);
            return data;
        }

        [Test]
        [TestCaseSource(nameof(ApiRoutes))]
        public void ApiRoutesStartWithTildaApi(string route)
        {
            if (route == "~/nuget/packages")
                return;

            route.Should().StartWith("~/api");
        }

        [Test]
        [TestCaseSource(nameof(WebRoutes))]
        public void WebRoutesStartWithTildaApp(string route)
        {
            route.Should().StartWith("~/app");
        }
    }
}