using Kernel.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kernel.Partitioning;

public class PartitionManager : IPartitionManager
{
    private readonly AppSettings _appSettings;

    public PartitionManager(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public int GetRandomPartition()
    {
        throw new NotImplementedException();
    }
}
