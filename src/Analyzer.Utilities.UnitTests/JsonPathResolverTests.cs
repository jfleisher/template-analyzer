﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Templates.Analyzer.Utilities.UnitTests
{
    [TestClass]
    public class JsonPathResolverTests
    {
        [DataTestMethod]
        [DataRow(null, DisplayName = "Null path")]
        [DataRow("", DisplayName = "Empty path")]
        public void Resolve_NullOrEmptyPath_ReturnsResolverWithOriginalJtoken(string path)
        {
            var jtoken = JObject.Parse("{ \"Property\": \"Value\" }");

            var resolver = new JsonPathResolver(jtoken, jtoken.Path);

            // Do twice to verify internal cache correctness
            for (int i = 0; i < 2; i++)
            {
                var results = resolver.Resolve(path).ToList();

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(jtoken, results[0].JToken);
            }
        }

        [DataTestMethod]
        [DataRow("nochildren", DisplayName = "Resolve one property")]
        [DataRow("onechildlevel.child2", DisplayName = "Resolve two properties deep, end of tree")]
        [DataRow("twochildlevels.child", DisplayName = "Resolve two properties deep, array returned")]
        [DataRow("twochildlevels.child2.lastprop", DisplayName = "Resolve three properties deep")]
        public void Resolve_JsonContainsPath_ReturnsResolverWithCorrectJtokenAndPath(string path)
        {
            JToken jtoken = JObject.Parse(
                @"{
                    ""NoChildren"": true,
                    ""OneChildLevel"": {
                        ""Child"": ""aValue"",
                        ""Child2"": 2
                    },
                    ""TwoChildLevels"": {
                        ""Child"": [ 0, 1, 2 ],
                        ""Child2"": {
                            ""LastProp"": true
                        }
                    },
                }");

            var resolver = new JsonPathResolver(jtoken, jtoken.Path);

            // Do twice to verify internal cache correctness
            for (int i = 0; i < 2; i++)
            {
                var results = resolver.Resolve(path).ToList();

                Assert.AreEqual(1, results.Count);

                // Verify correct property was resolved and resolver returns correct path
                Assert.AreEqual(path, results[0].JToken.Path, ignoreCase: true);
                Assert.AreEqual(path, results[0].Path, ignoreCase: true);
            }
        }

        [DataTestMethod]
        [DataRow("   ", DisplayName = "Whitespace path")]
        [DataRow(".", DisplayName = "Incomplete path")]
        [DataRow("Prop", DisplayName = "Non-existant path (single level)")]
        [DataRow("Property.Value", DisplayName = "Non-existant path (sub-level doesn't exist)")]
        [DataRow("Not.Existing.Property", DisplayName = "Non-existant path (multi-level, top level doesn't exist)")]
        public void Resolve_InvalidPath_ReturnsResolverWithNullJtokenAndCorrectResolvedPath(string path)
        {
            var jtoken = JObject.Parse("{ \"Property\": \"Value\" }");

            var resolver = new JsonPathResolver(jtoken, jtoken.Path);
            var expectedPath = $"{jtoken.Path}.{path}";

            // Do twice to verify internal cache correctness
            for (int i = 0; i < 2; i++)
            {
                var results = resolver.Resolve(path).ToList();

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(null, results[0].JToken);
                Assert.AreEqual(expectedPath, results[0].Path);
            }
        }

        [DataTestMethod]
        [DataRow(@"{ ""resources"": [ { ""type"": ""Microsoft.ResourceProvider/resource1"" } ] }", "Microsoft.ResourceProvider/resource1", new[] { 0 }, DisplayName = "1 (of 1) Matching Resource")]
        [DataRow(@"{ ""resources"": [ { ""type"": ""Microsoft.ResourceProvider/resource1"" }, { ""type"": ""Microsoft.ResourceProvider/resource1"" } ] }", "Microsoft.ResourceProvider/resource1", new[] { 0, 1 }, DisplayName = "2 (of 2) Matching Resources")]
        [DataRow(@"{ ""resources"": [ { ""type"": ""Microsoft.ResourceProvider/resource1"" }, { ""type"": ""Microsoft.ResourceProvider/resource2"" } ] }", "Microsoft.ResourceProvider/resource2", new[] { 1 }, DisplayName = "1 (of 2) Matching Resources")]
        [DataRow(@"{ ""resources"": [ { ""type"": ""Microsoft.ResourceProvider/resource1"" }, { ""type"": ""Microsoft.ResourceProvider/resource2"" } ] }", "Microsoft.ResourceProvider/resource3", new int[] { }, DisplayName = "0 (of 2) Matching Resources")]
        public void ResolveResourceType_JObjectWithExpectedResourcesArray_ReturnsResourcesOfCorrectType(string template, string resourceType, int[] matchingResourceIndexes)
        {
            var jToken = JObject.Parse(template);
            var resolver = new JsonPathResolver(jToken, jToken.Path);

            // Do twice to verify internal cache correctness
            for (int i = 0; i < 2; i++)
            {
                var resources = resolver.ResolveResourceType(resourceType).ToList();
                Assert.AreEqual(matchingResourceIndexes.Length, resources.Count);

                // Verify resources of correct type were returned
                for (int j = 0; j < matchingResourceIndexes.Length; j++)
                {
                    var resource = resources[j];
                    int resourceIndex = matchingResourceIndexes[j];
                    var expectedPath = $"resources[{resourceIndex}]";
                    Assert.AreEqual(expectedPath, resource.JToken.Path);
                }
            }
        }

        [DataTestMethod]
        [DataRow("string", DisplayName = "Resources is a string")]
        [DataRow(1, DisplayName = "Resources is an integer")]
        [DataRow(true, DisplayName = "Resources is a boolean")]
        [DataRow(new[] { 1, 2, 3 }, DisplayName = "Resources is an array of ints")]
        [DataRow(new[] { "1", "2", "3" }, DisplayName = "Resources is an array of ints")]
        public void ResolveResourceType_JObjectWithResourcesNotArrayOfObjects_ReturnsEmptyEnumerable(object value)
        {
            var jToken = JObject.Parse(
                string.Format("{{ \"resources\": {0} }}",
                JsonConvert.SerializeObject(value)));

            Assert.AreEqual(0, new JsonPathResolver(jToken, jToken.Path).ResolveResourceType("anything").Count());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullJToken_ThrowsException()
        {
            new JsonPathResolver(null, "");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullPath_ThrowsException()
        {
            new JsonPathResolver(new JObject(), null);
        }
    }
}
