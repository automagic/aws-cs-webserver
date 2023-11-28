using System;
using Pulumi;

public class LandingZoneArgs {
    public Input<string>? CidrBlock { get; set; }
    public Input<string>? MonthlyBudget { get; set; }
    public InputMap<string> Tags { get; set; }
    public string[] PublicSubnetCidrBlocks { get; set; }
    public string[] PrivateSubnetCidrBlocks { get; set;}

    public LandingZoneArgs()
    {
        PublicSubnetCidrBlocks = Array.Empty<string>();
        PrivateSubnetCidrBlocks = Array.Empty<string>();
        Tags = new InputMap<string>();
    }
}
