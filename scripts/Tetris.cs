namespace StorybrewScripts;

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;

internal class Tetris : StoryboardObjectGenerator
{
    [Configurable] public float BeatDivisor = 1;
    [Configurable] public int BlockLength = 4;
    [Configurable] public int Blocks = 1;
    Cell[,] cells;
    [Configurable] public float CellSize = 20;
    [Configurable] public Rgba32 Color = new(Vector3.One);
    [Configurable] public bool Dumb = false;
    [Configurable] public int EndTime = 0;
    [Configurable] public int GridHeight = 20;
    [Configurable] public int GridWidth = 10;
    [Configurable] public float Rotation = 0;

    [Group("Grid"), Configurable] public Vector2 ShadowOffset = new(4);

    [Group("Sprite"), Configurable] public string SpritePath = "sb/sq.png";

    [Configurable] public float SpriteScale = .625f;

    [Group("Timing"), Configurable] public int StartTime = 0;

    [Group("AI"), Configurable] public bool Wait = true;

    protected override void Generate()
    {
        var beatDuration = Beatmap.GetTimingPointAt(StartTime).BeatDuration;
        var timestep = beatDuration / BeatDivisor;

        cells = new Cell[GridWidth, GridHeight];
        for (var x = 0; x < GridWidth; ++x)
        for (var y = 0; y < GridHeight; ++y)
            cells[x, y] = new Cell { X = x, Y = y };

        for (float time = StartTime; time < EndTime; time += timestep)
        {
            for (var i = 0; i < Blocks; ++i) addBlock(time - timestep, time);
            if (clearLines(time, time + timestep)) time += Wait ? timestep : 0;
        }

        for (var x = 0; x < GridWidth; ++x)
        for (var y = 0; y < GridHeight; ++y)
            if (cells[x, y].HasSprite)
                killCell(EndTime, EndTime + timestep, x, y);
    }

    void addBlock(float startTime, float endTime)
    {
        var brightness = Random(.3f, 1);
        CommandColor color = new(Color.R * brightness, Color.G * brightness, Color.B * brightness);

        var heightMap = new int[GridWidth];
        var bottom = 0;
        for (var x = 0; x < GridWidth; ++x)
        {
            for (var y = 0; y < GridHeight; ++y)
            {
                if (cells[x, y].HasSprite) break;
                heightMap[x] = y;
            }

            bottom = Math.Max(bottom, heightMap[x]);
        }

        var dropX = Random(GridWidth);
        while (!Dumb && heightMap[dropX] != bottom) dropX = Random(GridWidth);

        var dropY = heightMap[dropX];

        fillCell(startTime, endTime, dropX, dropY, color);
        for (var i = 1; i < BlockLength; ++i)
        {
            int[] options = [0, 1, 2, 3];
            shuffle(options);

            foreach (var option in options)
            {
                var nextDropX = dropX;
                var nextDropY = dropY;

                switch (option)
                {
                    case 0: ++nextDropX; break;
                    case 1: ++nextDropY; break;
                    case 2: --nextDropX; break;
                    case 3: --nextDropY; break;
                }

                if (nextDropX < 0 || nextDropX >= GridWidth || nextDropY < 0 || nextDropY >= GridHeight) continue;
                if (cells[nextDropX, nextDropY].HasSprite) continue;
                if (heightMap[nextDropX] < nextDropY) continue;

                dropX = nextDropX;
                dropY = nextDropY;
                fillCell(startTime, endTime, dropX, dropY, color);
                break;
            }
        }
    }

    bool clearLines(float startTime, float endTime)
    {
        var anyCombo = false;
        var dropHeight = 0;
        for (var y = GridHeight - 1; y >= 0; y--)
        {
            var combo = true;
            for (var x = 0; x < GridWidth; ++x)
                if (!cells[x, y].HasSprite)
                {
                    combo = false;
                    break;
                }

            if (combo)
            {
                anyCombo = true;
                for (var x = 0; x < GridWidth; ++x) killCell(startTime, endTime, x, y);

                dropHeight++;
            }
            else if (dropHeight > 0)
                for (var x = 0; x < GridWidth; ++x)
                    if (cells[x, y].HasSprite)
                        dropCell(startTime, endTime, x, y, dropHeight);
        }

        return anyCombo;
    }

    void fillCell(float startTime, float endTime, int dropX, int dropY, Rgba32 color)
    {
        var shadow = GetLayer("Shadows").CreateSprite(SpritePath, OsbOrigin.TopCentre);
        var sprite = GetLayer("Blocks").CreateSprite(SpritePath, OsbOrigin.TopCentre);

        cells[dropX, dropY].Sprite = sprite;
        cells[dropX, dropY].Shadow = shadow;

        Vector2 targetPosition = new(dropX * CellSize, dropY * CellSize);
        var startPosition = targetPosition with { Y = targetPosition.Y - CellSize * GridHeight };

        sprite.Rotate(startTime, float.DegreesToRadians(Rotation));
        sprite.Scale(startTime, SpriteScale);
        sprite.Color(startTime, color);
        sprite.Move(OsbEasing.In, startTime, endTime, transform(startPosition), transform(targetPosition));

        shadow.Rotate(startTime, float.DegreesToRadians(Rotation));
        shadow.Scale(startTime, SpriteScale);
        shadow.Color(startTime, 0, 0, 0);
        shadow.Fade(startTime, .5f);
        shadow.Move(OsbEasing.In,
            startTime,
            endTime,
            transform(startPosition) + ShadowOffset,
            transform(targetPosition) + ShadowOffset);
    }

    void killCell(float startTime, float endTime, int dropX, int dropY)
    {
        var sprite = cells[dropX, dropY].Sprite;
        var shadow = cells[dropX, dropY].Shadow;
        cells[dropX, dropY].Sprite = null;
        cells[dropX, dropY].Shadow = null;

        sprite.Scale(startTime, endTime, SpriteScale, 0);
        sprite.Color(startTime, Color);

        shadow.Scale(startTime, endTime, SpriteScale, 0);
    }

    void dropCell(float startTime, float endTime, int dropX, int dropY, int dropHeight)
    {
        var sprite = cells[dropX, dropY].Sprite;
        var shadow = cells[dropX, dropY].Shadow;

        cells[dropX, dropY].Sprite = null;
        cells[dropX, dropY + dropHeight].Sprite = sprite;

        cells[dropX, dropY].Shadow = null;
        cells[dropX, dropY + dropHeight].Shadow = shadow;

        Vector2 targetPosition = new(dropX * CellSize, (dropY + dropHeight) * CellSize);
        var startPosition = targetPosition with { Y = dropY * CellSize };

        sprite.Move(OsbEasing.In, startTime, endTime, transform(startPosition), transform(targetPosition));
        shadow.Move(OsbEasing.In,
            startTime,
            endTime,
            transform(startPosition) + ShadowOffset,
            transform(targetPosition) + ShadowOffset);
    }

    Vector2 transform(Vector2 position)
        => Vector2.Transform(new(position.X - GridWidth * CellSize * .5f, position.Y - GridHeight * CellSize),
            Quaternion.CreateFromYawPitchRoll(0, float.DegreesToRadians(Rotation), 0));

    void shuffle(int[] array)
    {
        var n = array.Length;
        while (n > 1)
        {
            n--;
            var k = Random(n + 1);
            (array[n], array[k]) = (array[k], array[n]);
        }
    }

    public class Cell
    {
        internal OsbSprite Sprite, Shadow;
        internal int X, Y;
        internal bool HasSprite => Sprite is not null;
    }
}