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
    }
}
