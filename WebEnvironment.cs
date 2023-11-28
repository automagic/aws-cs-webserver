using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Pulumi.Aws.Ec2;
using Pulumi.Tls;

public class WebEnvironment : ComponentResource
{
    public List<Instance> Instances { get; set; }

    public SecurityGroup SecurityGroup { get; set; }

    public WebEnvironment(string name, WebEnvironmentArgs args, ComponentResourceOptions? options = null) 
        : base("custom:x:WebEnvirontment", name, options)
    {
        this.Instances = new List<Instance>();

        this.SecurityGroup = new SecurityGroup ($"{name}-sg", new SecurityGroupArgs {
            VpcId = args.VpcId,
            Ingress = new []{
                new Pulumi.Aws.Ec2.Inputs.SecurityGroupIngressArgs { Protocol = "TCP", FromPort = 22, ToPort = 22, CidrBlocks = new []{ args.VpcCidrBlock! }},
                new Pulumi.Aws.Ec2.Inputs.SecurityGroupIngressArgs { Protocol = "TCP", FromPort = 80, ToPort = 80, CidrBlocks = new []{ args.VpcCidrBlock! }}
            },
            Egress = new []{
                    new Pulumi.Aws.Ec2.Inputs.SecurityGroupEgressArgs { Protocol = "-1", FromPort = 0, ToPort = 0,  CidrBlocks = new []{ "0.0.0.0/0" }}
            }
        }, new CustomResourceOptions {
            Parent = this,
        });

        var sshKeyMaterial = new PrivateKey(name, new PrivateKeyArgs { Algorithm = "RSA", });

        var sshKey = new KeyPair(name, new KeyPairArgs {
            PublicKey = sshKeyMaterial.PublicKeyOpenssh, 
        }, new CustomResourceOptions {
            Parent = sshKeyMaterial,
        });
    

        var instanceCount = args.InstanceCount;
        for (var i = 0; i < instanceCount; i++) 
        {
            var webServer = new Instance($"{name}-server-{i}", new InstanceArgs {
                InstanceType = InstanceType.T3_Medium,
                AssociatePublicIpAddress = true,
                Ami = args.ImageId,
                SubnetId = args.SubnetIds.Apply(x => x.First()),
                KeyName = sshKey.KeyName,
                VpcSecurityGroupIds = new[] { this.SecurityGroup.Id },
                Tags = args.BaseTags ?? new InputMap<string>(),
            }, new CustomResourceOptions {
                Parent = this,
            });
            this.Instances.Add(webServer);
        }

        this.RegisterOutputs();
    }
}