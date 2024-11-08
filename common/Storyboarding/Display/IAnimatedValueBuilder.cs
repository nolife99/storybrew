namespace StorybrewCommon.Storyboarding.Display;

using Commands;

#pragma warning disable CS1591
public interface IAnimatedValueBuilder
{
    void Add(ICommand command);
    void StartDisplayLoop(LoopCommand loopCommand);
    void StartDisplayTrigger(TriggerCommand triggerCommand);
    void EndDisplayComposite();
}