using System;
using System.IO;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionInitializerTests
{
    [Fact]
    public void Initialize_WithCustomFolderPath_LoadsCollisionData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "collision_circle_circle.txt"), "1 1 1 1");
            File.WriteAllText(Path.Combine(tempDir, "collision_circle_rectangle.txt"), "");
            File.WriteAllText(Path.Combine(tempDir, "collision_rectangle_rectangle.txt"), "");

            var storage = CollisionInitializer.Initialize(tempDir);

            Assert.NotNull(storage);
            Assert.True(storage.ContainsKey(("circle", "circle")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Initialize_WithDefaultPath_UsesBaseDirectoryFallback()
    {
        var defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "collision_data");
        bool createdDir = false;

        if (!Directory.Exists(defaultDir))
        {
            Directory.CreateDirectory(defaultDir);
            createdDir = true;
        }

        try
        {
            File.WriteAllText(Path.Combine(defaultDir, "collision_circle_circle.txt"), "");
            File.WriteAllText(Path.Combine(defaultDir, "collision_circle_rectangle.txt"), "");
            File.WriteAllText(Path.Combine(defaultDir, "collision_rectangle_rectangle.txt"), "");

            var storage = CollisionInitializer.Initialize();

            Assert.NotNull(storage);
            Assert.True(storage.ContainsKey(("circle", "circle")));
        }
        finally
        {
            if (File.Exists(Path.Combine(defaultDir, "collision_circle_circle.txt")))
                File.Delete(Path.Combine(defaultDir, "collision_circle_circle.txt"));
            if (File.Exists(Path.Combine(defaultDir, "collision_circle_rectangle.txt")))
                File.Delete(Path.Combine(defaultDir, "collision_circle_rectangle.txt"));
            if (File.Exists(Path.Combine(defaultDir, "collision_rectangle_rectangle.txt")))
                File.Delete(Path.Combine(defaultDir, "collision_rectangle_rectangle.txt"));

            if (createdDir && Directory.Exists(defaultDir))
                Directory.Delete(defaultDir, true);
        }
    }
}
