using System.Linq;
using Pulumi;

public class WebserverStack : Stack
{
   public WebserverStack() : base(new StackOptions { ResourceTransformations = { TagTransformation } })
   {
      var config = new Pulumi.Config();

      var projectName = Deployment.Instance.ProjectName;

      var landingZone = new LandingZone(projectName, new LandingZoneArgs {
         CidrBlock = "10.0.0.0/20",
         PublicSubnetCidrBlocks = new [] { "10.0.0.0/24", "10.0.1.0/24", "10.0.2.0/24" },
      });

      var web = new WebEnvironment(projectName, new WebEnvironmentArgs {
         ImageId = Config.AmazonAmiId,
         InstanceCount =  config.GetInt32("instanceCount") ?? 1,
         VpcId = landingZone.Vpc.Id,
         VpcCidrBlock = landingZone.Vpc.CidrBlock,
         SubnetIds = landingZone.PublicSubnets.Select(x => x.Id).ToList(),
      });
   }

   private static ResourceTransformationResult? TagTransformation(ResourceTransformationArgs args)
   {
      if (Config.IsTaggable(args.Resource.GetResourceType()))
      {
         // Tranforms here

         return new ResourceTransformationResult(args.Args, args.Options);
      }
      return null;
   }
   internal record InstanceInfo(string HostName, Output<string> HostId);
}