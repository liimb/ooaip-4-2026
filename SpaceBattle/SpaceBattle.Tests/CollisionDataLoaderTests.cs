using System;
using System.IO;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionDataLoaderTests
{
    [Fact]
    public void LoadFromFiles_SuccessfullyLoadsDataAndCreatesCollisionTrees()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "collision_circle_circle.txt"), "1 2 3 4");
            File.WriteAllText(Path.Combine(tempDir, "collision_circle_rectangle.txt"), "5 6 7 8");
            File.WriteAllText(Path.Combine(tempDir, "collision_rectangle_rectangle.txt"), "9 10 11 12");

            var storage = CollisionDataLoader.LoadFromFiles(tempDir);

            Assert.NotNull(storage);
            Assert.True(storage.ContainsKey(("circle", "circle")));
            Assert.True(storage.ContainsKey(("circle", "rectangle")));
            Assert.True(storage.ContainsKey(("rectangle", "circle")));
            Assert.True(storage.ContainsKey(("rectangle", "rectangle")));

            Assert.True(storage[("circle", "circle")].Contains(new[] { 1, 2, 3, 4 }));
            Assert.True(storage[("rectangle", "circle")].Contains(new[] { -5, -6, -7, -8 }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadFromFiles_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            Assert.Throws<FileNotFoundException>(() => CollisionDataLoader.LoadFromFiles(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
