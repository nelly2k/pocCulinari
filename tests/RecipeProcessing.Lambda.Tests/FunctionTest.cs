using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

namespace RecipeProcessing.Lambda.Tests;

public class FunctionTest
{
    [Fact]
    public void TestToUpperFunction()
    {

        Environment.SetEnvironmentVariable("POWERTOOLS_METRICS_NAMESPACE", "AWSLambdaPowertools");
        // Invoke the lambda function and confirm the string was upper cased.
        var function = new Function();
        var context = new TestLambdaContext();
        var upperCase = function.FunctionHandler("hello world", context);

        Assert.Equal("HELLO WORLD", upperCase);
    }
}
