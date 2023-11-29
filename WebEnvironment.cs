using System;
using System.Collections.Generic;
using System.Text;
using Pulumi;
using Pulumi.Aws;
using Pulumi.Tls;
using LB = Pulumi.Aws.LB;
using Ec2 = Pulumi.Aws.Ec2;
using AutoScaling = Pulumi.Aws.AutoScaling;

public class WebEnvironment : ComponentResource
{
    public List<Ec2.Instance> Instances { get; set; }

    public Ec2.SecurityGroup SecurityGroup { get; set; }

    public WebEnvironment(string name, WebEnvironmentArgs args, ComponentResourceOptions? options = null) 
        : base("custom:x:WebEnvironment", name, options)
    {
        this.Instances = new List<Ec2.Instance>();

        this.SecurityGroup = new Ec2.SecurityGroup ($"{name}-sg", new Ec2.SecurityGroupArgs {
            VpcId = args.VpcId,
            Ingress = new []{
                new Ec2.Inputs.SecurityGroupIngressArgs { Protocol = "TCP", FromPort = 22, ToPort = 22, CidrBlocks = new []{ args.VpcCidrBlock! }},
                new Ec2.Inputs.SecurityGroupIngressArgs { Protocol = "TCP", FromPort = 80, ToPort = 80, CidrBlocks = new []{ args.VpcCidrBlock! }}
            },
            Egress = new []{
                    new Ec2.Inputs.SecurityGroupEgressArgs { Protocol = "-1", FromPort = 0, ToPort = 0,  CidrBlocks = new []{ "0.0.0.0/0" }}
            }
        }, new CustomResourceOptions {
            Parent = this,
        });

        var sshKeyMaterial = new PrivateKey(name, new PrivateKeyArgs { 
            Algorithm = "RSA", 
        }, new CustomResourceOptions {
             Parent = this 
        });

        var sshKey = new Ec2.KeyPair(name, new Ec2.KeyPairArgs {
            PublicKey = sshKeyMaterial.PublicKeyOpenssh, 
        }, new CustomResourceOptions {
            Parent = sshKeyMaterial,
        });

        var launchTemplate = new Ec2.LaunchTemplate($"{name}-lauch-config", new Ec2.LaunchTemplateArgs {
            NamePrefix = "web",
            InstanceType = "t3.medium",
            ImageId = args.ImageId,
            KeyName = sshKey.KeyName,
            VpcSecurityGroupIds = new[] { this.SecurityGroup.Id },
            Tags = args.BaseTags ?? new InputMap<string>(),
            UserData =  Convert.ToBase64String(Encoding.UTF8.GetBytes(@$"
#!/bin/bash
sudo yum update -y
sudo amazon-linux-extras install nginx1 -y 
sudo systemctl enable nginx
sudo systemctl start nginx    
            "))
        }, new CustomResourceOptions {
             Parent = this 
        });
    
        var asg = new AutoScaling.Group($"{name}-asg", new()
        {
            VpcZoneIdentifiers = args.SubnetIds!,
            DesiredCapacity = args.InstanceCount,
            MaxSize = args.InstanceCount,
            MinSize = 1,
            LaunchTemplate = new AutoScaling.Inputs.GroupLaunchTemplateArgs
            {
                Id = launchTemplate.Id,
                Version = "$Latest",
            },
        }, new CustomResourceOptions {
            Parent = this, 
        });

        var alb = new LB.LoadBalancer($"{name}-alb", new LB.LoadBalancerArgs {
            Internal = false,
            LoadBalancerType = "application",
            SecurityGroups = new [] { this.SecurityGroup.Id },
            Subnets = args.SubnetIds.Apply( sid => sid ),
            
        }, new CustomResourceOptions {
            Parent = this, 
        });

        var tg = new LB.TargetGroup($"{name}-tg", new LB.TargetGroupArgs {
            TargetType = "instance",
            Port = 80,
            Protocol = "HTTP",
            VpcId = args.VpcId,
        }, new CustomResourceOptions {
            Parent = this, 
        });

        var listerner = new LB.Listener($"{name}-frontend-listener", new LB.ListenerArgs { 
        
            LoadBalancerArn = alb.Arn,
            Port = 80,
            Protocol = "HTTP",
            DefaultActions = new[] {
                new LB.Inputs.ListenerDefaultActionArgs {
                    Type = "forward",
                    TargetGroupArn = tg.Arn,
                }
            }
        }, new CustomResourceOptions {
            Parent = alb, 
        });

        var attachment = new AutoScaling.Attachment($"{name}-alb-attachment", new() 
        {
            AutoscalingGroupName = asg.Name,
            LbTargetGroupArn = tg.Arn,
        }, new CustomResourceOptions {
            Parent = asg 
        });

        this.RegisterOutputs();
    }
}