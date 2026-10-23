using System;
using System.Collections.Generic;
using System.IO;
using App;
namespace SpaceBattle.Lib;

public static class CollisionInitializer
{
    public static IDictionary<(string, string), CollisionTree> Initialize(string dataFolderPath = "")
    {
        if (string.IsNullOrEmpty(dataFolderPath))
        {
            dataFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "collision_data");
        }

        return CollisionDataLoader.LoadFromFiles(dataFolderPath);
    }
}
