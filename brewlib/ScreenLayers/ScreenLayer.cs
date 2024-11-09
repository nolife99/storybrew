namespace BrewLib.ScreenLayers;

using System;
using Graphics;
using Input;
using osuTK.Input;

public abstract class ScreenLayer : InputAdapter, IDisposable
{
    public enum State
    {
        Hidden, FadingIn, Active,
        FadingOut
    }

    readonly InputDispatcher inputDispatcher = new(), innerInputDispatcher = new();

    public State CurrentState = State.Hidden;

    bool hasStarted;
    public float MinTween;

    protected float TransitionInDuration = .25f, TransitionOutDuration = .25f, TransitionProgress;
    public ScreenLayerManager Manager { get; set; }
    public bool HasFocus { get; private set; }

    public virtual bool IsPopup => false;
    public bool IsActive => HasFocus && CurrentState is State.FadingIn or State.Active;

    public bool IsExiting { get; private set; }
    public InputHandler InputHandler => inputDispatcher;

    public virtual void Load()
    {
        inputDispatcher.Add(innerInputDispatcher);
        inputDispatcher.Add(this);
    }

    public void GainFocus() => HasFocus = true;
    public void LoseFocus() => HasFocus = false;

    protected void AddInputHandler(InputHandler handler) => innerInputDispatcher.Add(handler);

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
    public virtual void Draw(DrawContext drawContext, double tween) { }

    public virtual void OnStart() { }
    public virtual void OnTransitionIn() { }
    public virtual void OnTransitionOut() { }
    public virtual void OnActive() { }
    public virtual void OnHidden() { }
    public virtual void OnExit() { }

    public virtual void Close() => Exit();

    public override bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.Key is not Key.Escape) return base.OnKeyDown(e);
        Close();
        return true;
    }

    public void Exit() => Exit(false);
    public void Exit(bool skipTransition)
    {
        if (IsExiting) return;
        IsExiting = true;

        OnExit();

        if (skipTransition || TransitionOutDuration == 0) Manager.Remove(this);
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

    public bool IsDisposed { get; private set; }
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing && HasFocus) throw new InvalidOperationException(GetType().Name + " still has focus");
        IsDisposed = true;
    }
    public void Dispose() => Dispose(true);

    #endregion
}