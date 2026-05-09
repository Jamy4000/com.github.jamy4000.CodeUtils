using System.Collections.Generic;

namespace CodeUtils
{
    public abstract class FSM<TState, TStateEnum> : System.IDisposable
        where TState : class, IFSMState<TStateEnum>
        where TStateEnum : struct, System.Enum
    {
        public TState ActiveState { get; private set; }

        private readonly Dictionary<TStateEnum, TState> _states;
        protected IReadOnlyDictionary<TStateEnum, TState> States => _states;

        private readonly List<TStateEnum> _currentStatePotentialTransitions = new(16);
        private readonly HashSet<TStateEnum> _pendingTransitions = new();

        private System.Action<TStateEnum> _stateStartedEvent;
        private System.Action<TStateEnum> _stateEndedEvent;

        private readonly System.Action<TStateEnum> _cachedAddPendingStateCallback;
        private readonly System.Action _cachedExitCurrentStateCallback;

        private bool _explicitExitRequestReceived;
        private bool _isTransitioning;

        protected FSM(TState defaultState, List<TState> states)
        {
            _cachedExitCurrentStateCallback = ExitCurrentState;
            _cachedAddPendingStateCallback = AddPendingState;

            _states = new Dictionary<TStateEnum, TState>(states.Count);

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                RegisterNewState(states[stateIndex]);
            }

            StartNewState(defaultState, default);
        }

        public virtual void ManualUpdate()
        {
            ActiveState.UpdateState();
        }

        public virtual void ManualLateUpdate()
        {
            if (_isTransitioning || (!_explicitExitRequestReceived && !ActiveState.CanBeExited()))
                return;

            TStateEnum previousStateEnum = ActiveState.StateEnum;

            foreach (var stateEnum in _currentStatePotentialTransitions)
            {
                if (_pendingTransitions.Contains(stateEnum))
                {
                    _isTransitioning = true;
                    StopCurrentState();
                    StartNewState(_states[stateEnum], previousStateEnum);
                    _isTransitioning = false;
                    return;
                }
            }

            // No explicit pending request — check if any registered transition state can be entered
            foreach (var stateEnum in _currentStatePotentialTransitions)
            {
                TState state = _states[stateEnum];
                if (state.CanBeEntered())
                {
                    _isTransitioning = true;
                    StopCurrentState();
                    StartNewState(state, previousStateEnum);
                    _isTransitioning = false;
                    return;
                }
            }

#if DEBUG
            throw new System.Exception($"State {previousStateEnum} couldn't be exited. Pending Transitions: {string.Join(", ", _pendingTransitions)}");
#endif
        }

        public void Dispose()
        {
            StopCurrentState();
            ActiveState = null;

            foreach (TState state in _states.Values)
            {
                state.Dispose();
            }

            _states.Clear();
            _currentStatePotentialTransitions.Clear();
            _pendingTransitions.Clear();
        }

        public void RegisterNewState(TState stateToRegister)
        {
            if (!_states.TryAdd(stateToRegister.StateEnum, stateToRegister))
                throw new System.Exception($"State {stateToRegister.StateEnum} is already registered.");
        }

        public void RegisterNewState(TStateEnum stateEnum, TState state)
        {
            if (!_states.TryAdd(stateEnum, state))
                throw new System.Exception($"State {stateEnum} is already registered.");
        }

        public void UnregisterState(TState stateToRemove)
        {
            if (!_states.Remove(stateToRemove.StateEnum))
                throw new System.Exception($"State {stateToRemove.StateEnum} isn't registered.");
        }

        public void UnregisterState(TStateEnum stateToRemove)
        {
            if (!_states.Remove(stateToRemove))
                throw new System.Exception($"State {stateToRemove} isn't registered.");
        }

        public bool TryGetState(TStateEnum stateEnum, out TState state)
        {
            return _states.TryGetValue(stateEnum, out state);
        }

        protected virtual void StartNewState(TState newState, TStateEnum oldState)
        {
            ActiveState = newState;
            _explicitExitRequestReceived = false;

            ActiveState.StartState(oldState);
            ActiveState.RequestToExitCurrentState += _cachedExitCurrentStateCallback;

            IReadOnlyList<TStateEnum> allTransitions = ActiveState.GetTransitionsStates();
            foreach (var stateEnum in allTransitions)
            {
                if (!_states.TryGetValue(stateEnum, out TState state))
                    continue;

                _currentStatePotentialTransitions.Add(stateEnum);
                state.RequestEnterState += _cachedAddPendingStateCallback;
            }

            _pendingTransitions.Clear();
            _stateStartedEvent?.Invoke(ActiveState.StateEnum);
        }

        protected virtual void StopCurrentState()
        {
            ActiveState.EndState();
            ActiveState.RequestToExitCurrentState -= _cachedExitCurrentStateCallback;

            foreach (var state in _currentStatePotentialTransitions)
            {
                _states[state].RequestEnterState -= _cachedAddPendingStateCallback;
            }

            _currentStatePotentialTransitions.Clear();
            _stateEndedEvent?.Invoke(ActiveState.StateEnum);
        }

        protected virtual void AddPendingState(TStateEnum newStateEnum)
        {
            if (!ActiveState.HasPossibleTransitionsTo(newStateEnum))
                throw new System.Exception($"State {newStateEnum} isn't part of the possible transitions for {ActiveState.StateEnum}.");

            _pendingTransitions.Add(newStateEnum);
        }

        private void ExitCurrentState()
        {
            foreach (var stateEnum in _currentStatePotentialTransitions)
            {
                if (_pendingTransitions.Contains(stateEnum))
                    return;

                TState state = _states[stateEnum];
                if (state.CanBeEntered())
                {
                    _pendingTransitions.Add(stateEnum);
                    _explicitExitRequestReceived = true;
                    return;
                }
            }

            throw new System.Exception($"State {ActiveState.StateEnum} couldn't be exited.");
        }

        public void RegisterStateStartedCallback(System.Action<TStateEnum> callback) => _stateStartedEvent += callback;
        public void RegisterStateEndedCallback(System.Action<TStateEnum> callback) => _stateEndedEvent += callback;
        public void UnregisterStateStartedCallback(System.Action<TStateEnum> callback) => _stateStartedEvent -= callback;
        public void UnregisterStateEndedCallback(System.Action<TStateEnum> callback) => _stateEndedEvent -= callback;
    }
}