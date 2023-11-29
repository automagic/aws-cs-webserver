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
        }, new CustomResourceOptions {
            Parent = this,
        });

        var available = GetAvailabilityZones
            .Invoke(new (){ State = "available" });

        var zones = new []{ "us-west-2a", "us-west-2b", "us-west-2c"};

        this.Vpc = new Vpc(name, new VpcArgs{
            CidrBlock = args.CidrBlock,
            EnableDnsHostnames = true,
            Tags = args.Tags ?? new InputMap<string>{},
        }, new CustomResourceOptions {
            Parent = this,
        });

        var internetGateway = new InternetGateway(name, new InternetGatewayArgs{
            VpcId = this.Vpc.Id,
            Tags = args.Tags ?? new InputMap<string>{},
        }, new CustomResourceOptions {
            Parent = this.Vpc,
        });

        var publicSubnetRouteTable = new RouteTable($"{name}-public", new RouteTableArgs{
            VpcId = this.Vpc.Id,
            Tags = args.Tags ?? new InputMap<string>{},
        }, new CustomResourceOptions { Parent = this.Vpc });


        var privateSubnetRouteTable = new RouteTable($"{name}-private", new RouteTableArgs {
            VpcId = this.Vpc.Id,
            Tags = args.Tags ?? new InputMap<string>{},
        }, new CustomResourceOptions {
            Parent = this.Vpc,
        });
        
        var publicSubnetRoute = new Route($"{name}-public", new RouteArgs {
            RouteTableId = publicSubnetRouteTable.Id,
            DestinationCidrBlock = "0.0.0.0/0",
            GatewayId = internetGateway.Id,
        }, new CustomResourceOptions {
            Parent = publicSubnetRouteTable,
        });

        for (var i = 0; i < args.PublicSubnetCidrBlocks.Length; i++) 
        {
            var az = available.Apply(res => res.Names[i]);

            var publicSubnet = new Subnet($"{name}-public-{i}", new SubnetArgs {
                VpcId = this.Vpc.Id,
                AvailabilityZone = zones[i],
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
                    Domain = "vpc",
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions {
                    DependsOn = internetGateway,
                    Parent = this,
                });

                var natGateway = new NatGateway($"{name}-public-{i}", new NatGatewayArgs {
                    SubnetId = publicSubnet.Id,
                    AllocationId = natEip.Id,
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions {
                    Parent = publicSubnet,
                });

                var privateSubnet = new Subnet($"{name}-private-{i}", new SubnetArgs {
                    VpcId = this.Vpc.Id,
                    AvailabilityZone = zones[i],
                    CidrBlock = args.PrivateSubnetCidrBlocks[i],
                    MapPublicIpOnLaunch = false,
                    Tags = args.Tags ?? new InputMap<string>{},
                }, new CustomResourceOptions { 
                    Parent = this.Vpc,
                    DeleteBeforeReplace = true,
                });
                this.PrivateSubnets.Add(privateSubnet);

                var privateSubnetRoute = new Route($"{name}-private-{i}", new RouteArgs {
                    RouteTableId = privateSubnetRouteTable.Id,
                    DestinationCidrBlock = "0.0.0.0/0",
                    NatGatewayId = natGateway.Id,
                }, new CustomResourceOptions {
                    Parent = privateSubnetRouteTable,
                });

                var privateSubnetRouteTableAssociation = new RouteTableAssociation($"{name}-private-{i}", new RouteTableAssociationArgs{
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