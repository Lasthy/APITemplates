using Kernel.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Configuration;

public class AppSettings : DynamicSettingsSection
{
    public string Environment => GetValue<string>(nameof(Environment));
    public int PartitionAmount => GetValue<int>(nameof(PartitionAmount));

    public AppSettings(string source, IJsonSerializer jsonSerializer) : base(source, jsonSerializer)
    {
    }
}
