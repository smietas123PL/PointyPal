using System;

namespace PointyPal.Infrastructure;

public class AppStateManager
{
    private CompanionState _currentState = CompanionState.FollowingCursor;

    public CompanionState CurrentState => _currentState;

    public event EventHandler<(CompanionState State, string Reason)>? StateChanged;

    public void SetState(CompanionState newState, string reason)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
            StateChanged?.Invoke(this, (_currentState, reason));
        }
    }

    public void CancelToFollowingCursor(string reason)
    {
        if (IsInFlightState() || _currentState == CompanionState.Processing)
        {
            SetState(CompanionState.FollowingCursor, reason);
        }
    }

    public bool IsInFlightState()
    {
        return _currentState == CompanionState.FlyingToTarget || 
               _currentState == CompanionState.PointingAtTarget || 
               _currentState == CompanionState.ReturningToCursor;
    }
}
