namespace StorybrewCommon.Storyboarding
{
    ///<summary> Layers for an <see cref="StoryboardObject"/>. </summary>
    ///<remarks> Constructs a new layer with the given name. </remarks>
    ///<param name="name"> The name of the layer. </param>
    public abstract class StoryboardLayer(string name) : StoryboardSegment
    {
        ///<summary> The name of the layer. </summary>
        public string Name { get; } = name;
    }
}