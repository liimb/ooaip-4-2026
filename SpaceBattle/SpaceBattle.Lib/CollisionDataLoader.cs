using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App;

namespace SpaceBattle.Lib;

public static class CollisionDataLoader
{

    public static IDictionary<(string, string), CollisionTree> LoadFromFiles(string dataFolderPath)
    {
        var storage = new Dictionary<(string, string), CollisionTree>();

        var circleCircleData = LoadCollisionData(Path.Combine(dataFolderPath, "collision_circle_circle.txt"));
        var circleRectangleData = LoadCollisionData(Path.Combine(dataFolderPath, "collision_circle_rectangle.txt"));
        var rectangleRectangleData = LoadCollisionData(Path.Combine(dataFolderPath, "collision_rectangle_rectangle.txt"));

        storage[("circle", "circle")] = new CollisionTree(circleCircleData);
        storage[("circle", "rectangle")] = new CollisionTree(circleRectangleData);
        storage[("rectangle", "circle")] = new CollisionTree(circleRectangleData.Select(ReverseState));
        storage[("rectangle", "rectangle")] = new CollisionTree(rectangleRectangleData);

        return storage;
    }

    private static List<(int x, int y, int vx, int vy)> LoadCollisionData(string filename)
    {
        var data = new List<(int, int, int, int)>();

        if (!File.Exists(filename))
            throw new FileNotFoundException($"Файл с данными не найден: {filename}");

        foreach (var line in File.ReadLines(filename))
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4)
            {
                data.Add((
                    int.Parse(parts[0]),
                    int.Parse(parts[1]),
                    int.Parse(parts[2]),
                    int.Parse(parts[3])
                ));
            }
        }

        return data;
    }

    private static (int, int, int, int) ReverseState((int x, int y, int vx, int vy) state)
    {
        return (-state.x, -state.y, -state.vx, -state.vy);
    }
}
