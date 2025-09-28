namespace StorybrewCommon.Storyboarding3d;

using System;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Commands;

///<summary> Represents a 3D scene with a tree of nodes. </summary>
public class Scene3d
{
    ///<summary> Represents the scene's base node. </summary>
    public readonly Node3d Root = new();

    ///<summary> Adds a 3D object to the scene's node. </summary>
    public void Add(Object3d child) => Root.Add(child);

    /// <summary>
    /// Generates a 3D scene from <paramref name="startTime"/> to <paramref name="endTime"/> with given ticking period
    /// <paramref name="timeStep"/>.
    /// </summary>
    public void Generate(Camera camera, StoryboardSegment segment, float startTime, float endTime, float timeStep)
    {
        Root.GenerateTreeSprite(segment);
        for (var time = startTime; time < endTime + 5; time += timeStep) Root.GenerateTreeStates(time, camera);
        Root.GenerateTreeCommands();
    }

    /// <summary>
    /// Generates a 3D scene from <paramref name="startTime"/> to <paramref name="endTime"/> with an ticking period based
    /// on the beatmap's timing point and <paramref name="divisor"/>.
    /// </summary>
    public void Generate(Camera camera,
        StoryboardSegment segment,
        float startTime,
        float endTime,
        Beatmap beatmap,
        int divisor = 8)
    {
        Root.GenerateTreeSprite(segment);
        beatmap.ForEachTick((int)startTime,
            (int)endTime,
            divisor,
            (_, time, _, _) => Root.GenerateTreeStates(time, camera));

        Root.GenerateTreeCommands();
    }

    /// <summary>
    /// Generates a looping 3D scene from <paramref name="startTime"/> to <paramref name="endTime"/> with given ticking
    /// period <paramref name="timeStep"/> and loop count <paramref name="loopCount"/>.
    /// </summary>
    public void Generate(Camera camera,
        StoryboardSegment segment,
        float startTime,
        float endTime,
        float timeStep,
        int loopCount,
        Action<LoopCommand, OsbSprite> action = null)
    {
        Root.GenerateTreeSprite(segment);
        for (var time = startTime; time < endTime + 5; time += timeStep) Root.GenerateTreeStates(time, camera);
        Root.GenerateTreeLoopCommands(startTime, endTime, loopCount, action);
    }
}