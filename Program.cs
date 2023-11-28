using System.Linq;
using Pulumi;

return await Deployment.RunAsync(() =>
{   

   var config = new Pulumi.Config();

   var projectName = Deployment.Instance.ProjectName;

   var landingZone = new LandingZone(projectName, new LandingZoneArgs {
      CidrBlock = "10.0.0.0/20",
      PublicSubnetCidrBlocks = new [] { "10.0.0.0/24", "10.0.1.0/24", "10.0.2.0/24" },
   });

   var web = new WebEnvironment(projectName, new WebEnvironmentArgs {
     ImageId = Config.UbuntuAmiId,
     InstanceCount =  config.GetInt32("instanceCount") ?? 1,
     SubnetIds = landingZone.PublicSubnets.Select(x => x.Id).ToArray(),
     VpcId = landingZone.Vpc.Id,
     VpcCidrBlock = landingZone.Vpc.CidrBlock,
   });

});

internal record InstanceInfo(string HostName, Output<string> HostId);
