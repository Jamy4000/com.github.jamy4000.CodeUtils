using System.Collections.Generic;

namespace UnityTechnologies.CodeUtils
{
    public abstract class FSM<TState, TStateEnum> : System.IDisposable
        where TState : class, IFSMState<TStateEnum>
        where TStateEnum : struct, System.Enum
    {
        public TState ActiveState { get; private set; }

        protected readonly Dictionary<TStateEnum, TState> States;

        private readonly List<TStateEnum> _currentStatePotentialTransitions = new(16);
        private readonly List<TStateEnum> _pendingTransitions = new(4);

        private System.Action<TStateEnum> _stateStartedEvent;
        private System.Action<TStateEnum> _stateEndedEvent;

        private readonly System.Action<TStateEnum> _cachedAddPendingStateCallback;
        private readonly System.Action _cachedExitCurrentStateCallback;

        private bool _explicitExitRequestReceived;

        protected FSM(TState defaultState, List<TState> states)
        {
            _cachedExitCurrentStateCallback = ExitCurrentState;
            _cachedAddPendingStateCallback = AddPendingState;

            States = new Dictionary<TStateEnum, TState>(states.Count);

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                RegisterNewState(states[stateIndex]);
            }

            StartNewState(defaultState, default);
        }

        public void ManualUpdate()
        {
            ActiveState.UpdateState();
        }

        public virtual void ManualLateUpdate()
        {
            if (!_explicitExitRequestReceived && !ActiveState.CanBeExited())
                return;

            // Used as a End of Frame Coroutine to check pending states and chose the one with the highest priority
            for (int stateIndex = 0; stateIndex < _currentStatePotentialTransitions.Count; stateIndex++)
            {
                // we go through each transitions of the state to check for the priority of each state.
                TStateEnum stateEnum = _currentStatePotentialTransitions[stateIndex];
                if (_pendingTransitions.Contains(stateEnum))
                {
                    StopCurrentState();
                    StartNewState(States[stateEnum], ActiveState.StateEnum);
                    _pendingTransitions.Clear();
                    return;
                }
            }

            // if no pending state was waiting to be entered, we check the potential exit states
            List<TStateEnum> potentialExitStates = ActiveState.GetTransitionsStates();
            for (int stateIndex = 0; stateIndex < potentialExitStates.Count; stateIndex++)
            {
                TState state = States[potentialExitStates[stateIndex]];
                if (state.CanBeEntered())
                {
                    StopCurrentState();
                    StartNewState(state, ActiveState.StateEnum);
                    _pendingTransitions.Clear();
                    return;
                }
            }

#if DEBUG
            string transitions = string.Empty;
            for (int i = 0; i < _pendingTransitions.Count; i++)
            {
                transitions += _pendingTransitions[i] + ", ";
            }
            throw new System.Exception($"State {ActiveState.StateEnum} couldn't be exited. Pending Transitions: {transitions}");
#endif
        }

        public void Dispose()
        {
            StopCurrentState();
            ActiveState = null;

            foreach (TState state in States.Values)
            {
                state.OnDestroy();
            }

            States.Clear();
            _currentStatePotentialTransitions.Clear();
            _pendingTransitions.Clear();
        }

        public void RegisterNewState(TState stateToRegister)
        {
            if (States.ContainsKey(stateToRegister.StateEnum))
                throw new System.Exception($"State {stateToRegister.StateEnum} is already registered.");

            States.Add(stateToRegister.StateEnum, stateToRegister);
        }

        public void RegisterNewState(TStateEnum stateEnum, TState state)
        {
            if (States.ContainsKey(stateEnum))
                throw new System.Exception($"State {stateEnum} is already registered.");

            States.Add(stateEnum, state);
        }

        public void UnregisterState(TState stateToRemove)
        {
            if (!States.Remove(stateToRemove.StateEnum))
                throw new System.Exception($"State {stateToRemove.StateEnum} isn't registered.");
        }

        public void UnregisterState(TStateEnum stateToRemove)
        {
            if (!States.Remove(stateToRemove))
                throw new System.Exception($"State {stateToRemove} isn't registered.");
        }

        public bool TryGetState(TStateEnum stateEnum, out TState state)
        {
            return States.TryGetValue(stateEnum, out state);
        }

        protected virtual void StartNewState(TState newState, TStateEnum oldState)
        {
            // StartState and prepare new State
            ActiveState = newState;
            ActiveState.IsActiveState = true;
            _explicitExitRequestReceived = false;

            newState.StartState(oldState);
            newState.RequestToExitCurrentState += _cachedExitCurrentStateCallback;

            _currentStatePotentialTransitions.AddRange(newState.GetTransitionsStates());
            // some states may be set as potential transitions, but may not be registered in the FSM yet.
            List<TStateEnum> finalTransitions = new List<TStateEnum>(_currentStatePotentialTransitions.Count);

            for (int stateIndex = 0; stateIndex < _currentStatePotentialTransitions.Count; stateIndex++)
            {
                TStateEnum stateEnum = _currentStatePotentialTransitions[stateIndex];
                if (!States.TryGetValue(stateEnum, out TState state))
                    continue;

                finalTransitions.Add(stateEnum);
                state.RequestEnterState += AddPendingState;
            }

            _currentStatePotentialTransitions.Clear();
            _currentStatePotentialTransitions.AddRange(finalTransitions);
            _pendingTransitions.Clear();

            _stateStartedEvent?.Invoke(newState.StateEnum);
        }

        protected virtual void StopCurrentState()
        {
            // EndState and Clean old stateEnum
            ActiveState.EndState();
            ActiveState.IsActiveState = false;
            ActiveState.RequestToExitCurrentState -= ExitCurrentState;

            for (int stateIndex = 0; stateIndex < _currentStatePotentialTransitions.Count; stateIndex++)
            {
                States[_currentStatePotentialTransitions[stateIndex]].RequestEnterState -= _cachedAddPendingStateCallback;
            }

            _currentStatePotentialTransitions.Clear();

            _stateEndedEvent?.Invoke(ActiveState.StateEnum);
        }

        protected virtual void AddPendingState(TStateEnum newStateEnum)
        {
            if (!ActiveState.HasPossibleTransitionsTo(newStateEnum))
                throw new System.Exception($"State {newStateEnum} isn't part of the possible transitions for {ActiveState.StateEnum}.");

            // The transition was already requested
            if (!_pendingTransitions.Contains(newStateEnum))
                _pendingTransitions.Add(newStateEnum);
        }

        private void ExitCurrentState()
        {
            for (int stateIndex = 0; stateIndex < _currentStatePotentialTransitions.Count; stateIndex++)
            {
                TStateEnum stateEnum = _currentStatePotentialTransitions[stateIndex];

                // The transition was already requested
                if (_pendingTransitions.Contains(stateEnum))
                    return;

                TState state = States[stateEnum];
                if (state.CanBeEntered())
                {
                    _pendingTransitions.Add(stateEnum);
                    _explicitExitRequestReceived = true;
                    return;
                }
            }

            throw new System.Exception($"State {ActiveState.StateEnum} couldn't be exited.");
        }

        public void RegisterStateStartedCallback(System.Action<TStateEnum> callback)
        {
            _stateStartedEvent += callback;
        }

        public void RegisterStateEndedCallback(System.Action<TStateEnum> callback)
        {
            _stateEndedEvent += callback;
        }

        public void UnregisterStateStartedCallback(System.Action<TStateEnum> callback)
        {
            _stateStartedEvent -= callback;
        }

        public void UnregisterStateEndedCallback(System.Action<TStateEnum> callback)
        {
            _stateEndedEvent -= callback;
        }
    }
}