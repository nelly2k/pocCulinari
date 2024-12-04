using Amazon.Lambda.Core;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;
using Amazon.S3;
using Amazon.Lambda.S3Events;
using Amazon.DynamoDBv2;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace RecipeProcessing.Lambda;


public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonBedrockRuntime _bedrockClient;

    public Function()
    {
        _s3Client = new AmazonS3Client();
        _dynamoDbClient = new AmazonDynamoDBClient();
        _bedrockClient = new AmazonBedrockRuntimeClient();

    }

    [Logging(LogEvent = true)]
    [Tracing]
    public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
    {

        var s3Event = evnt.Records?[0].S3;
        if (s3Event == null)
        {
            return "No S3 event found";

        }

        Logger.LogInformation($"No S3 event found");
        var bucketName = s3Event.Bucket.Name;
        var objectKey = s3Event.Object.Key;

        try
        {
            // Read recipe from S3
            var getResponse = await _s3Client.GetObjectAsync(bucketName, objectKey);
            using var reader = new StreamReader(getResponse.ResponseStream);
            var recipeText = await reader.ReadToEndAsync();

            // Process with Bedrock
            var processedRecipe = await ProcessRecipeWithBedrock(recipeText);

            // Store in DynamoDB
            await StoreProcessedRecipe(objectKey, processedRecipe);
            Logger.LogInformation($"Recipe processed successfully");
            return "Recipe processed successfully";
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing recipe: {ex.Message}");
            throw;
        }
    }

    [Metrics(CaptureColdStart = true)]
    [Tracing(SegmentName = "ProcessRecipeWithBedrock Method")]
    private async Task<string> ProcessRecipeWithBedrock(string recipeText)
    {
        var request = new InvokeModelRequest
        {
            ModelId = "anthropic.claude-v2",
            Body = JsonSerializer.Serialize(new
            {
                prompt = $@"Parse this recipe into a structured, machine-readable format:
                Recipe Text:
                {recipeText}
                
                Provide JSON output with:
                - Detailed step-by-step actions
                - Estimated time for each step
                - Possible tools and their time impacts
                - Parallel processing potential
                - Cleaning requirements",
                max_tokens_to_sample = 4000,
                temperature = 0.3
            })
        };

        var response = await _bedrockClient.InvokeModelAsync(request);
        return Encoding.UTF8.GetString(response.Body.ToArray());
    }


    [Metrics(CaptureColdStart = true)]
    [Tracing(SegmentName = "StoreProcessedRecipe Method")]
    private async Task StoreProcessedRecipe(string recipeId, string processedRecipe)
    {
        var tableName = Environment.GetEnvironmentVariable("RECIPES_TABLE");

        var request = new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                { "RecipeId", new AttributeValue { S = recipeId } },
                { "Recipe", new AttributeValue { S = processedRecipe } }
            }
        };
        await _dynamoDbClient.PutItemAsync(request);

    }


}
