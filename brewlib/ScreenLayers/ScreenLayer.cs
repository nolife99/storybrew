namespace BrewLib.ScreenLayers;

using System;
using Graphics;
using Input;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

public abstract class ScreenLayer : InputAdapter, IDisposable
{
    public enum State
    {
        Hidden, FadingIn, Active, FadingOut
    }

    readonly InputDispatcher inputDispatcher = new(), innerInputDispatcher = new();

    bool hasStarted;

    protected float TransitionInDuration = .25f, TransitionOutDuration = .25f, TransitionProgress;

    public State CurrentState { get; private set; } = State.Hidden;
    public ScreenLayerManager Manager { get; set; }
    public bool HasFocus { get; private set; }

    public virtual bool IsPopup => false;

    public bool IsExiting { get; private set; }
    public IInputHandler InputHandler => inputDispatcher;

    public virtual void Load()
    {
        inputDispatcher.Add(innerInputDispatcher);
        inputDispatcher.Add(this);
    }

    public void GainFocus() => HasFocus = true;
    public void LoseFocus() => HasFocus = false;

    protected void AddInputHandler(IInputHandler handler) => innerInputDispatcher.Add(handler);

    public virtual void Resize(int width, int height) { }

    public virtual void Update(bool isTopFocus, bool isCovered)
    {
        if (!hasStarted && !IsExiting && !isCovered)
        {
            OnStart();
            hasStarted = true;
        }

        if (IsExiting)
        {
            if (CurrentState is not State.FadingOut) OnTransitionOut();

            CurrentState = State.FadingOut;
            if (updateTransition(Manager.TimeSource.Elapsed, TransitionOutDuration, -1)) return;

            OnHidden();
            Manager.Remove(this);
        }
        else if (isCovered)
        {
            if (updateTransition(Manager.TimeSource.Elapsed, TransitionOutDuration, -1))
            {
                if (CurrentState is not State.FadingOut) OnTransitionOut();
                CurrentState = State.FadingOut;
            }
            else
            {
                if (CurrentState is not State.Hidden) OnHidden();
                CurrentState = State.Hidden;
            }
        }
        else
        {
            if (updateTransition(Manager.TimeSource.Elapsed, TransitionInDuration, 1))
            {
                if (CurrentState is not State.FadingIn) OnTransitionIn();
                CurrentState = State.FadingIn;
            }
            else
            {
                if (CurrentState is not State.Active) OnActive();
                CurrentState = State.Active;
            }
        }
    }

    public virtual void FixedUpdate() { }
    public virtual void Draw(DrawContext drawContext) { }

    public virtual void OnStart() { }
    public virtual void OnTransitionIn() { }
    public virtual void OnTransitionOut() { }
    public virtual void OnActive() { }
    public virtual void OnHidden() { }
    public virtual void OnExit() { }

    public virtual void Close() => Exit();

    public override bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.Key is not Keys.Escape) return base.OnKeyDown(e);

        Close();
        return true;
    }

    public void Exit()
    {
        if (IsExiting) return;

        IsExiting = true;

        OnExit();

        if (TransitionOutDuration == 0) Manager.Remove(this);
    }

    bool updateTransition(float delta, float duration, int direction)
    {
        var progress = duration > 0 ? delta / duration : 1;
        TransitionProgress += progress * direction;

        switch (TransitionProgress)
        {
            case <= 0:
                TransitionProgress = 0;
                return false;

            case >= 1:
                TransitionProgress = 1;
                return false;

            default: return true;
        }
    }

    #region IDisposable Support

    protected bool IsDisposed { get; private set; }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing && HasFocus) throw new InvalidOperationException(GetType().Name + " still has focus");

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}