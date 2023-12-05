using Pulumi;
using Pulumi.Aws;
using Pulumi.Aws.Inputs;
using Pulumi.Aws.Route53;

public class WebserverStack : Stack
{
   [Output] 
   public Output<string> WebUrl { get; set; }

   public WebserverStack()
   {
      var projectName = Deployment.Instance.ProjectName;
      var stackName = Deployment.Instance.StackName;

      var config = new Pulumi.Config();

      InputMap<string> tags = new InputMap<string> {
         { "Environment", config.Require("environment") },
         { "Project", projectName },
         { "Stack", stackName},
         { "ManagedBy", "Pulumi"},
      };

      var awsProvider = new Provider("aws-provider", new ProviderArgs {
         DefaultTags = new ProviderDefaultTagsArgs
         {
            Tags = tags,
         }
      });

      var options = new ComponentResourceOptions { Provider = awsProvider };


      var currentIdentity = GetCallerIdentity.Invoke();

      var budget = new Pulumi.Aws.Budgets.Budget(projectName, new()
      {

         AccountId = currentIdentity.Apply(result => result.AccountId),
         BudgetType = "COST",
         LimitAmount = config.Get("budget") ?? "100.00",
         LimitUnit = "USD",
         TimePeriodStart = "2010-01-01_00:00",
         TimeUnit = "MONTHLY",
         CostFilters = new() {
            new Pulumi.Aws.Budgets.Inputs.BudgetCostFilterArgs() {
               Name = "TagKeyValue",
               Values = "user:Project$ASGDemo"
            }
         }
      }, new CustomResourceOptions()
      {
         Provider = awsProvider
      });

      var vpcStack = new StackReference(config.Get("vpcStack")!);

      var hostedZoneName = config.Get("hostedZoneName");
      var subdomain = $"{config.Get("subDomain")}.{hostedZoneName}";

      var hostedZone = Output.Create(GetZone.InvokeAsync(new GetZoneArgs
      {
         Name = $"{hostedZoneName}.",
         PrivateZone = false
      }));

      var certificate = new AcmDnsValidatedCertificate(projectName, new()
      {
         ZoneId = hostedZone.Apply(z => z.ZoneId),
         Subdomain = subdomain,
      }, options);

      var web = new WebEnvironment(projectName, new()
      {
         ImageId = Helpers.AmazonAmiId,
         InstanceCount = config.GetInt32("instanceCount") ?? 1,
         VpcId = vpcStack.RequireOutput("VpcId").Apply(o => (string)o),
         VpcCidrBlock = vpcStack.RequireOutput("CidrBlock").Apply(o => (string)o),
         PublicSubnetIds = vpcStack.RequireOutput("PublicSubnetIds").AsArray<string>(),
         PrivateSubnetIds = vpcStack.RequireOutput("PrivateSubnetIds").AsArray<string>(),
         CertificateArn = certificate.certificateArn,
         ZoneId = hostedZone.Apply(z => z.ZoneId),
         Subdomain = subdomain,
         AsgTags = tags
      }, options);

      this.WebUrl = Output.Create($"https://{subdomain}");
   }
}