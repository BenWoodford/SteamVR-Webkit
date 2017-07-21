using System.Collections.Generic;

public class EnUs
{
    public string name { get; set; }
    public string description { get; set; }
}

public class Strings
{
    public EnUs en_us { get; set; }
}

public class Application
{
    public string app_key { get; set; }
    public string launch_type { get; set; }
    public string binary_path_windows { get; set; }
    public bool is_dashboard_overlay { get; set; }
    public Strings strings { get; set; }
}

public class VRManifest
{
    public string source { get; set; }
    public List<Application> applications { get; set; }
}