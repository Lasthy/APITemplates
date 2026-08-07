using Kernel.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Configuration;

public class InstanceSettings : DynamicSettingsSection
{
    public (int Start, int End) Partition
    {
        get => GetValue<(int Start, int End)>(nameof(Partition));
    }
    public Dictionary<string, bool> ActiveModules
    {
        get => GetValue<Dictionary<string, bool>>(nameof(ActiveModules));
    }

    public InstanceSettings(string source, IJsonSerializer jsonSerializer) : base(source, jsonSerializer)
    {
    }
}
