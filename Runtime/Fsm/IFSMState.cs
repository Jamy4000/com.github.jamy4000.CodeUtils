using System.Collections.Generic;

namespace CodeUtils
{
    public interface IFSMState : System.IDisposable
    {
        event System.Action RequestToExitCurrentState;

        bool CanBeEntered();
        bool CanBeExited();
    }

    public interface IFSMState<TStateEnum> : IFSMState
        where TStateEnum : struct, System.Enum
    {
        TStateEnum StateEnum { get; }

        event System.Action<TStateEnum> RequestEnterState;

        bool HasPossibleTransitionsTo(TStateEnum stateEnum);

        void StartState(TStateEnum previousState);

        void UpdateState();

        void EndState();

        IReadOnlyList<TStateEnum> GetTransitionsStates();
    }
}
