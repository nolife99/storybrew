namespace StorybrewCommon.Storyboarding.Display;

using Commands;

internal interface IAnimatedValueBuilder
{
    void Add(ICommand command);
    void StartDisplayLoop(LoopCommand loopCommand);
    void StartDisplayTrigger(TriggerCommand triggerCommand);
    void EndDisplayComposite();
}