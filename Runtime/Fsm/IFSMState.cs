using System.Collections.Generic;

namespace UnityTechnologies.CodeUtils
{
    public interface IFSMState
    {
        bool IsActiveState { get; set; }
        System.Action RequestToExitCurrentState { get; set; }

        bool CanBeEntered();
        bool CanBeExited();

        void OnDestroy();
    }

    public interface IFSMState<TStateEnum> : IFSMState
        where TStateEnum : struct, System.Enum
    {
        TStateEnum StateEnum { get; }

        System.Action<TStateEnum> RequestEnterState { get; set; }

        bool HasPossibleTransitionsTo(TStateEnum stateEnum);

        void StartState(TStateEnum previousState);

        void UpdateState();

        void EndState();

        List<TStateEnum> GetTransitionsStates();
    }
}