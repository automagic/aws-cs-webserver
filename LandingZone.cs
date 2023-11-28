using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Pulumi.Aws;
using Pulumi.Aws.Ec2;

public class LandingZone : ComponentResource
{
    public Vpc Vpc { get; set; }

    public List<Subnet> PublicSubnets { get; set; }

    public List<Subnet> PrivateSubnets { get; set; }

    public LandingZone(string name, LandingZoneArgs args, ComponentResourceOptions? options = null) 
        : base("custom:x:LandingZone", name, options)
    {
        this.PublicSubnets = new List<Subnet>();
        this.PrivateSubnets = new List<Subnet>();
        
        var currentIdentity = GetCallerIdentity.Invoke();

        var budget = new Pulumi.Aws.Budgets.Budget(name, new Pulumi.Aws.Budgets.BudgetArgs{
            AccountId = currentIdentity.Apply(result => result.AccountId),
            BudgetType = "COST",
            LimitAmount = args.MonthlyBudget ?? "500.00",
            LimitUnit = "USD",
            TimePeriodStart = "2010-01-01_00:00",
            TimeUnit = "MONTHLY",
        });

        var azs = GetAvailabilityZones.Invoke(new (){ State = "available" }).Apply(result => result.ZoneIds.ToArray());

        this.Vpc = new Vpc(name, new VpcArgs{
            CidrBlock = args.CidrBlock,
            EnableDnsHostnames = true,
            Tags = args.Tags ?? new InputMap<string>{},
        });

        var internetGateway = new InternetGateway(name, new InternetGatewayArgs{
            VpcId = this.Vpc.Id,
            Tags = args.Tags ?? new InputMap<string>{},
        });

        var publicSubnetRouteTable = new RouteTable($"{name}-public", new RouteTableArgs{
            VpcId = this.Vpc.Id,
            Tags = args.Tags ?? new InputMap<string>{},
        }, new CustomResourceOptions { Parent = this.Vpc });

        var publicSubnetRoute = new Route($"{name}-public", new RouteArgs {
            RouteTableId = publicSubnetRouteTable.Id,
            DestinationCidrBlock = "0.0.0.0/0",
            GatewayId = internetGateway.Id,
        });

        for (var i = 0; i < args.PublicSubnetCidrBlocks.Length; i++) 
        {
            var az = azs.Apply(l => l[i]);

            var publicSubnet = new Subnet($"{name}-public-{i}", new SubnetArgs {
                VpcId = this.Vpc.Id,
                AvailabilityZoneId = az,
                CidrBlock = args.PublicSubnetCidrBlocks[i],
                MapPublicIpOnLaunch = true,
                Tags = args.Tags ?? new InputMap<string>{},
            }, new CustomResourceOptions {
                Parent = this.Vpc,
                DeleteBeforeReplace = true
            });

            var publicSubnetRouteTableAssociation = new RouteTableAssociation($"{name}-public-{i}", new RouteTableAssociationArgs {
                SubnetId = publicSubnet.Id,
                RouteTableId = publicSubnetRouteTable.Id,
            }, new CustomResourceOptions {
                Parent = publicSubnet,
            });

            PublicSubnets.Add(publicSubnet);

            /** 
             * Private Subnets
             */

             if (args.PrivateSubnetCidrBlocks.Length > 0)
             {
                var natEip = new Eip($"{name}-public-{i}", new EipArgs {
                    Vpc = true,
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions {
                    DependsOn = internetGateway,
                    Parent = this,
                });

                var natGateway = new NatGateway($"{name}-public-{i}", new NatGatewayArgs {
                    SubnetId = publicSubnet.Id,
                    AllocationId = natEip.Id,
                    Tags = args.Tags ?? new InputMap<string>{},
                });

                var privateSubnet = new Subnet($"{name}-private-{i}", new SubnetArgs {
                    VpcId = this.Vpc.Id,
                    AvailabilityZoneId = az,
                    CidrBlock = args.PrivateSubnetCidrBlocks[i],
                    MapPublicIpOnLaunch = false,
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions { 
                    Parent = this.Vpc,
                    DeleteBeforeReplace = true,
                });
                this.PrivateSubnets.Add(privateSubnet);

                var privateSubnetRouteTable = new RouteTable($"{name}-private-{i}", new RouteTableArgs {
                    VpcId = this.Vpc.Id,
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions {
                    Parent = this.Vpc,
                });

                var privateSubnetRoute = new Route($"", new RouteArgs {
                    RouteTableId = privateSubnetRouteTable.Id,
                    DestinationCidrBlock = "0.0.0.0/0",
                    NatGatewayId = natGateway.Id,
                }, new CustomResourceOptions {
                    Parent = privateSubnetRouteTable,
                });

                var privateSubnetRouteTableAssociation = new RouteTableAssociation("", new RouteTableAssociationArgs{
                    SubnetId = privateSubnet.Id,
                    RouteTableId = privateSubnetRouteTable.Id,
                }, new CustomResourceOptions {
                    Parent = privateSubnet,
                });
            }
        }

        this.RegisterOutputs();
    }
}