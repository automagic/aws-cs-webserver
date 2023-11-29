using System;
using Pulumi;

public class WebEnvironmentArgs {

    [Input("ImageId", false, false)]
    public Input<string>? ImageId { get; set; }

    [Input("InstanceCount", false, false)]
    public int InstanceCount { get; set; }

    [Input("VpcId", false, false)] 
    public Input<string>? VpcId { get; set; }

    [Input("VpcCidrBlock", false, false)]
    public Input<string>? VpcCidrBlock { get; set; }

    [Input("BaseTags", false, false)]
    public InputMap<string>? BaseTags { get; set; }

    [Input("SubnetIds", false, false)]
    public InputList<string>? SubnetIds { get; set; }

    [Input("AvailabilityZones", false, false)]
    public InputList<string>? AvailabilityZones {get; set;}
}
