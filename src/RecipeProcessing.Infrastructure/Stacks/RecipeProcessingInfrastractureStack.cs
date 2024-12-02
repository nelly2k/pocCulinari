using Amazon.CDK;
using Constructs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda.EventSources;
using System.Collections.Generic;

namespace RecipeProcessingInfrastructure
{
    public class RecipeProcessingInfrastructureStack : Stack
    {
        internal RecipeProcessingInfrastructureStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {

            var recipeBucket = new Bucket(this, "RecipeSourceBucket", new BucketProps
            {
                Versioned = true,
                Encryption = BucketEncryption.S3_MANAGED,
                RemovalPolicy = RemovalPolicy.DESTROY,
                AutoDeleteObjects = true
            });

            // DynamoDB Table for Processed Recipes
            var recipeTable = new Table(this, "ProcessedRecipesTable", new TableProps
            {
                PartitionKey = new Attribute
                {
                    Name = "recipe_id",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // IAM Role for Lambda Function
            var lambdaRole = new Role(this, "RecipeProcessingLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = new[] {
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                }
            });

            // Add specific permissions
            recipeBucket.GrantRead(lambdaRole);
            recipeTable.GrantWriteData(lambdaRole);

            // Add Bedrock invoke permission
            lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "bedrock:InvokeModel" },
                Resources = new[] { "*" }
            }));


            // Lambda Function
            var processingLambda = new Function(this, "RecipeProcessingFunction", new FunctionProps
            {
                Handler = "RecipeProcessing.Lambda::RecipeProcessing.Function::FunctionHandler",
                Code = Code.FromAsset("../RecipeProcessing.Lambda/bin/Release/net8.0/linux-x64/publish/"),
                Role = lambdaRole,
                Timeout = Duration.Seconds(300),
                MemorySize = 1024,
                Environment = new Dictionary<string, string>
                {
                    { "RECIPES_TABLE", recipeTable.TableName },
                    { "RECIPES_BUCKET", recipeBucket.BucketName }
                },
                Runtime = Runtime.DOTNET_8
            });

            // S3 Trigger for Lambda
            var s3EventSource = new S3EventSource(recipeBucket, new S3EventSourceProps
            {
                Events = new[] { 
                    S3EventType.OBJECT_CREATED_PUT, 
                    S3EventType.OBJECT_CREATED_POST 
                },
                Filters = new[]
                {
                    new NotificationKeyFilter 
                    { 
                        Prefix = "recipes/", 
                        Suffix = ".txt" 
                    }
                }
            });

            processingLambda.AddEventSource(s3EventSource);

            // Outputs
            new CfnOutput(this, "RecipeBucketName", new CfnOutputProps
            {
                Value = recipeBucket.BucketName,
                Description = "Recipe Source Bucket"
            });

            new CfnOutput(this, "ProcessedRecipesTableName", new CfnOutputProps
            {
                Value = recipeTable.TableName,
                Description = "Processed Recipes DynamoDB Table"
            });

        }
    }
}
