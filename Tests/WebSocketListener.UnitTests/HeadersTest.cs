using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using vtortola.WebSockets.Http;
using Xunit;

namespace WebSocketListener.UnitTests
{
    public class HeadersTest
    {
        [Theory,
        InlineData("Host:This is MyHost", "Host", new[] { "This is MyHost" }),
        InlineData(" Host:This is MyHost  ", "Host", new[] { "This is MyHost" }),
        InlineData(" Host :This is MyHost", "Host", new[] { "This is MyHost" }),
        InlineData(" Host : This is MyHost", "Host", new[] { "This is MyHost" }),
        InlineData(" Host  :  This is MyHost", "Host", new[] { "This is MyHost" }),
        InlineData(" Host  :  This is MyHost ", "Host", new[] { "This is MyHost" }),
        InlineData(" Host  :  This is MyHost   ", "Host", new[] { "This is MyHost" }),
        InlineData("  Host  :  This is MyHost   ", "Host", new[] { "This is MyHost" }),
        InlineData("  Host  :  This is , MyHost   ", "Host", new[] { "This is , MyHost" }), // host is atomic header and should be split
        InlineData("MyCustomHeader:MyValue1,MyValue2", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1,MyValue2", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1 ,MyValue2", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1, MyValue2", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1 , MyValue2", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1 , MyValue2 ", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1 , MyValue2  ", "MyCustomHeader", new[] { "MyValue1", "MyValue2" }),
        InlineData("MyCustomHeader: MyValue1 , MyValue2 , MyValue3", "MyCustomHeader", new[] { "MyValue1", "MyValue2", "MyValue3" }),
        ]
        public void TryParseAndAddRequestHeaderTest(string header, string expectedKey, string[] expectedValues)
        {
            var headers = new Headers<RequestHeader>();

            headers.TryParseAndAdd(header);
            var actualValues = headers.GetValues(expectedKey).ToArray();

            Assert.Equal(expectedValues, actualValues);
        }

        [Fact]
        public void ClearTest()
        {
            // clear filled
            var dict = new Headers<RequestHeader>();
            dict.Set(RequestHeader.Accept, "value");
            dict.Set("Custom1", "value");
            Assert.Equal(2, dict.Count);
            dict.Clear();
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains(RequestHeader.Accept));
            Assert.False(dict.Contains("Accept"));
            Assert.False(dict.Contains("Custom1"));

            // clear new
            dict = new Headers<RequestHeader>();
            dict.Clear();
            dict = new Headers<RequestHeader>(new NameValueCollection());
            dict.Clear();
            dict = new Headers<RequestHeader>(new Dictionary<string, string>());
            dict.Clear();

            // clear empty
            dict = new Headers<RequestHeader>();
            dict.Set(RequestHeader.Trailer, "value");
            dict.Remove(RequestHeader.Trailer);
            dict.Clear();
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains(RequestHeader.Trailer));

            // clear cleared
            dict = new Headers<RequestHeader>();
            dict.Clear();
            dict.Clear();
        }
        [Fact]
        public void ConstructorTest()
        {
            // empty
            var dict1 = new Headers<RequestHeader>();

            // namevalue collection
            var dict2 = new Headers<RequestHeader>(new NameValueCollection
            {
                {
                    "Custom", "Value1"
                },
                {
                    "Via", "Value2"
                }
            });
            Assert.Equal(2, dict2.Count);
            Assert.Equal(dict2["Custom"], "Value1");
            Assert.Equal(dict2[RequestHeader.Via], "Value2");

            // dictionary
            var dict3 = new Headers<RequestHeader>(new Dictionary<string, string>
            {
                {
                    "Custom", "Value1"
                },
                {
                    "Via", "Value2"
                }
            });
            Assert.Equal(dict2["Custom"], "Value1");
            Assert.Equal(dict2[RequestHeader.Via], "Value2");
        }

        [Fact]
        public void ContainsTest()
        {
            // contains known
            var dict = new Headers<RequestHeader>();
            var coll = dict as ICollection<KeyValuePair<string, string>>;

            dict.Set(RequestHeader.Via, "value");

            Assert.True(dict.Contains("Via"), "containskey failed");
            Assert.False(dict.Contains("Host"), "containskey failed");
            Assert.True(coll.Contains(new KeyValuePair<string, string>("Via", "value")), "contains(key,value) failed");
            Assert.False(coll.Contains(new KeyValuePair<string, string>("Via", "value1")), "contains(key,value) failed");

            // contains new/missing/existing custom
            dict = new Headers<RequestHeader>();
            coll = dict;

            dict.Set("Custom1", "value");

            Assert.True(dict.Contains("Custom1"), "containskey failed");
            Assert.False(dict.Contains("Custom2"), "containskey failed");
            Assert.True(coll.Contains(new KeyValuePair<string, string>("Custom1", "value")), "contains(key,value) failed");
            Assert.False(coll.Contains(new KeyValuePair<string, string>("Custom1", "value1")), "contains(key,value) failed");

            // contains new/missing/existing mixed
            dict = new Headers<RequestHeader>();
            coll = dict;

            dict.Set(RequestHeader.Via, "value");
            dict.Set("Custom1", "value");

            Assert.True(dict.Contains("Custom1"), "containskey failed");
            Assert.False(dict.Contains("Custom2"), "containskey failed");
            Assert.True(coll.Contains(new KeyValuePair<string, string>("Custom1", "value")), "contains(key,value) failed");
            Assert.False(coll.Contains(new KeyValuePair<string, string>("Custom1", "value1")), "contains(key,value) failed");

            Assert.True(dict.Contains("Via"), "containskey failed");
            Assert.True(dict.Contains(RequestHeader.Via), "contains failed");
            Assert.False(dict.Contains("Host"), "containskey failed");
            Assert.True(coll.Contains(new KeyValuePair<string, string>("Via", "value")), "contains(key,value) failed");
            Assert.False(coll.Contains(new KeyValuePair<string, string>("Via", "value1")), "contains(key,value) failed");

            // contains empty
            dict = new Headers<RequestHeader>();
            coll = dict;

            Assert.False(dict.Contains("Custom2"), "containskey failed");
            Assert.False(coll.Contains(new KeyValuePair<string, string>("Custom1", "value1")), "contains(key,value) failed");
        }

        [Fact]
        public void EnumerateTest()
        {
            // enum new/missing/existing known
            var knownDict = new Headers<RequestHeader>();

            knownDict.Set(RequestHeader.Via, "via");
            knownDict.Set(RequestHeader.Trailer, "trailer");
            knownDict.Set(RequestHeader.Te, "te header");
            knownDict.Remove(RequestHeader.Te);

            Assert.Equal(2, knownDict.Count);

            // enum new/missing/existing custom
            var custDict = new Headers<RequestHeader>();

            custDict.Set("Custom1", "value1");
            custDict.Set("Custom2", "value2");
            custDict.Set("Custom3", "value3");
            custDict.Remove("Custom3");

            Assert.Equal(2, custDict.Count);

            // enum empty
            var dict = new Headers<RequestHeader>();

            Assert.Equal(0, dict.Count);

            // get allkeys
            dict = new Headers<RequestHeader>();

            dict.Set(RequestHeader.Via, "via");
            dict.Set(RequestHeader.Trailer, "trailer");
            dict.Set("Custom1", "value1");
            dict.Set("Custom2", "value2");

            var allKeys = (dict as IDictionary<string, string>).Keys.ToList();
            var allValues = (dict as IDictionary<string, string>).Values.ToList();

            var expectedKeys = new[]
            {
                "Via", "Trailer", "Custom1", "Custom2"
            };
            var expectedValues = new[]
            {
                "via", "trailer", "value1", "value2"
            };

            Assert.Contains(expectedKeys, allKeys.Contains);
            Assert.Contains(expectedValues, allValues.Contains);
        }

        [Fact]
        public void GetSetTest()
        {
            // set new/existing known
            // get missing/existing known
            var dict = new Headers<RequestHeader>();

            dict.Set(RequestHeader.Trailer, "value");
            Assert.Equal(1, dict.Count);
            Assert.Equal(dict[RequestHeader.Trailer], "value");
            Assert.False(dict.Contains(RequestHeader.AcceptCharset));

            dict.Set(RequestHeader.Trailer, "value2");
            Assert.Equal(1, dict.Count);
            Assert.Equal(dict[RequestHeader.Trailer], "value2");
            Assert.False(dict.Contains(RequestHeader.AcceptCharset));

            dict.Set(RequestHeader.Trailer, "valueA");
            dict.Set(RequestHeader.Via, "valueB");
            Assert.Equal(2, dict.Count);
            Assert.Equal(dict[RequestHeader.Trailer], "valueA");
            Assert.Equal(dict[RequestHeader.Via], "valueB");
            Assert.False(dict.Contains(RequestHeader.AcceptCharset));

            // set new/existing custom
            // get missing/existing custom
            dict = new Headers<RequestHeader>();

            dict.Set("Custom1", "value");
            Assert.Equal(1, dict.Count);
            Assert.Equal(dict["Custom1"], "value");

            dict.Set("Custom1", "value2");
            Assert.Equal(1, dict.Count);
            Assert.Equal(dict["Custom1"], "value2");

            dict.Set("Custom1", "valueA");
            dict.Set("Custom2", "valueB");
            Assert.Equal(2, dict.Count);
            Assert.Equal(dict["Custom1"], "valueA");
            Assert.Equal(dict["Custom2"], "valueB");
            Assert.False(dict.Contains("Custom3"));

            // get missing/existing custom case-insensitive
            Assert.Equal(dict["custom1"], "valueA");
            Assert.Equal(dict["custom2"], "valueB");
            Assert.False(dict.Contains("custom3"));

            // set new/existing mixed
            // get missing/existing mixed
            dict = new Headers<RequestHeader>();
            dict.Set("Custom1", "value");
            dict.Set(RequestHeader.Trailer, "value");
            Assert.Equal(2, dict.Count);
            Assert.Equal(dict["Custom1"], "value");
            Assert.Equal(dict[RequestHeader.Trailer], "value");

            dict.Set("Custom1", "value2");
            dict.Set(RequestHeader.Trailer, "value2");
            Assert.Equal(2, dict.Count);
            Assert.Equal(dict["Custom1"], "value2");
            Assert.Equal(dict[RequestHeader.Trailer], "value2");

            dict.Set("Custom1", "valueA");
            dict.Set("Custom2", "valueB");
            dict.Set(RequestHeader.Trailer, "valueA");
            dict.Set(RequestHeader.Via, "valueB");
            Assert.Equal(4, dict.Count);
            Assert.Equal(dict[RequestHeader.Trailer], "valueA");
            Assert.Equal(dict[RequestHeader.Via], "valueB");
            Assert.Equal(dict["Custom1"], "valueA");
            Assert.Equal(dict["Custom2"], "valueB");

            // get missing/existing mixed case-insensitive
            Assert.Equal(dict["custom1"], "valueA");
            Assert.Equal(dict["custom2"], "valueB");
            Assert.Equal(dict["trailer"], "valueA");
            Assert.Equal(dict["via"], "valueB");

            // get empty
            dict = new Headers<RequestHeader>();
            Assert.Equal(0, dict.Count);
            Assert.Equal(dict.Get(RequestHeader.Via), "");
            Assert.Equal(dict.Get("Via"), "");
            Assert.Equal(dict.Get("Custom"), "");
            Assert.False(dict.Contains(RequestHeader.Via));
            Assert.False(dict.Contains("Via"));
            Assert.False(dict.Contains("Custom"));
        }

        [Fact]
        public void RemoveTest()
        {
            // remove new/missing/existing known
            var dict = new Headers<RequestHeader>();
            dict.Set(RequestHeader.Accept, "value");
            Assert.Equal(1, dict.Count);
            Assert.Equal("value", dict[RequestHeader.Accept]);
            dict.Remove(RequestHeader.Accept);
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains(RequestHeader.Accept));

            //  remove missing value
            dict.Remove(RequestHeader.Accept);
            dict.Remove(RequestHeader.Via);

            //  remove modified value
            dict.Set(RequestHeader.Accept, "value");
            dict.Set("accept", "value2");
            Assert.Equal(1, dict.Count);
            Assert.Equal("value2", dict[RequestHeader.Accept]);
            dict.Remove(RequestHeader.Accept);
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains(RequestHeader.Accept));

            // remove new/missing/existing custom
            dict = new Headers<RequestHeader>();
            dict.Set("Custom1", "value");
            Assert.Equal(1, dict.Count);
            Assert.Equal("value", dict["Custom1"]);
            dict.Remove("Custom1");
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains("Custom1"));

            //  remove missing value
            dict.Remove("Custom1");
            dict.Remove("Custom2");

            //  remove modified value
            dict.Set("Custom1", "value");
            dict.Set("Custom1", "value2");
            Assert.Equal(1, dict.Count);
            Assert.Equal("value2", dict["Custom1"]);
            dict.Remove("Custom1");
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains("Custom1"));

            // remove new/missing/existing mixed
            dict = new Headers<RequestHeader>();
            dict.Set("Custom1", "value");
            dict.Set(RequestHeader.Accept, "value");
            Assert.Equal(2, dict.Count);
            Assert.Equal("value", dict["Custom1"]);
            Assert.Equal("value", dict[RequestHeader.Accept]);
            dict.Remove("Custom1");
            dict.Remove(RequestHeader.Accept);
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains("Custom1"));
            Assert.False(dict.Contains(RequestHeader.Accept));

            //  remove missing value
            dict.Remove("Custom1");
            dict.Remove("Custom2");
            dict.Remove(RequestHeader.Accept);
            dict.Remove(RequestHeader.Via);

            //  remove modified value
            dict.Set("Custom1", "value");
            dict.Set("Custom1", "value2");
            dict.Set(RequestHeader.Accept, "value");
            dict.Set(RequestHeader.Accept, "value2");
            Assert.Equal(2, dict.Count);
            Assert.Equal("value2", dict["Custom1"]);
            Assert.Equal("value2", dict[RequestHeader.Accept]);
            dict.Remove("Custom1");
            dict.Remove(RequestHeader.Accept);
            Assert.Equal(0, dict.Count);
            Assert.False(dict.Contains("Custom1"));
            Assert.False(dict.Contains(RequestHeader.Accept));

            // remove non existing
            dict = new Headers<RequestHeader>();
            dict.Remove(RequestHeader.Host);
            dict.Remove("Custom");
        }
    }
}
