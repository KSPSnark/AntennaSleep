
using System.Text;

namespace AntennaSleep
{
    public class ModuleDeployableSleep : PartModule
    {
        private const double NOT_SLEEPING = 0;

        private ModuleDeployablePart deployable;
        private int lastSleepSecondsRemaining = -1;
        private State lastState;
        private State currentState;

        /// <summary>
        /// Stores the time, in UTC seconds, when the part will wake up. 0 if not currently asleep.
        /// </summary>
        [KSPField(isPersistant = true)]
        public double wakeTime = NOT_SLEEPING;

        /// <summary>
        /// Text to display in right-click menu while sleeping.
        /// </summary>
        [KSPField(guiName = "Wake In", guiActive = false, guiActiveEditor = false)]
        public string sleepDisplay = string.Empty;
        private BaseField SleepDisplayField { get { return Fields["sleepDisplay"]; } }

        /// <summary>
        /// Determines how long the part will sleep when the "Sleep" button is pressed.
        /// </summary>
        [KSPField(guiName = "Sleep Minutes", guiActive = false, guiActiveEditor = false, isPersistant = true),
         UI_FloatRange(scene = UI_Scene.All, affectSymCounterparts = UI_Scene.Editor, controlEnabled = true, minValue = 0.5F, maxValue = 20F, stepIncrement = 0.5F)]
        public float sleepMinutes = 5F;
        private BaseField SleepMinutesField { get { return Fields["sleepMinutes"]; } }

        [KSPAction("Sleep")]
        public void DoSleepAction(KSPActionParam actionParam)
        {
            SleepDeployable();
        }
        private BaseAction SleepAction { get { return Actions["DoSleepAction"]; } }

        /// <summary>
        /// Right-click menu item for putting the part to sleep.
        /// </summary>
        [KSPEvent(active = true, guiActive = true, guiActiveEditor = false, guiName = "Sleep")]
        public void DoSleepEvent()
        {
            SleepDeployable();
        }
        private BaseEvent SleepEvent { get { return Events["DoSleepEvent"]; } }

        /// <summary>
        /// Called when the module is starting up.
        /// </summary>
        /// <param name="state"></param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            deployable = FindDeployable();
            if (deployable == null)
            {
                Logging.Error("No ModuleDeployablePart found for " + Logging.ToString(part) + ", ignoring");
            }
            lastState = new State(this);
            currentState = new State(this);
            SetState();
        }

        /// <summary>
        /// Called on every frame.
        /// </summary>
        void Update()
        {
            currentState.Update();
            bool isOverdue = currentState.IsOverdue;
            if (isOverdue || !currentState.IsSameState(lastState))
            {
                State swap = currentState;
                currentState = lastState;
                lastState = swap;

                if (isOverdue)
                {
                    WakeDeployable();
                }
                else
                {
                    Logging.Log("Updating " + Logging.ToString(part) + " state");
                    SetState();
                }
            }
            if (SleepSecondsRemaining > 0)
            {
                UpdateSleepTimeRemaining();
            }
        }

        /// <summary>
        /// Gets whether the deployable part is currently sleeping.
        /// </summary>
        private bool IsSleeping
        {
            get { return wakeTime > NOT_SLEEPING; }
        }

        /// <summary>
        /// Gets whether the deployable part is currently deployed.
        /// </summary>
        private ModuleDeployablePart.DeployState DeployState
        {
            get
            {
                return (deployable == null) ? ModuleDeployablePart.DeployState.BROKEN : deployable.deployState;
            }
        }

        /// <summary>
        /// Puts the part to sleep.
        /// </summary>
        private void SleepDeployable()
        {
            Logging.Log(Logging.ToString(part) + " sleeping for " + sleepMinutes + " minutes");
            wakeTime = Planetarium.GetUniversalTime() + 60.0 * sleepMinutes;
            deployable.Retract();
            SetState();
        }

        /// <summary>
        /// Wakes up the part when the sleep time expires.
        /// </summary>
        private void WakeDeployable()
        {
            if (!deployable.CanMove)
            {
                // It wants to wake up, but physically can't!  Do nothing.
                Logging.Warn("Can't wake " + Logging.ToString(part) + " (can't currently move)");
                return;
            }
            wakeTime = NOT_SLEEPING;
            switch (DeployState)
            {
                case ModuleDeployablePart.DeployState.RETRACTED:
                    // need to wake up
                    Logging.Log("Waking " + Logging.ToString(part));
                    wakeTime = NOT_SLEEPING;
                    deployable.Extend();
                    break;
                default:
                    // nothing to do
                    Logging.Log("Can't wake " + Logging.ToString(part) + " (it's already " + DeployState + ")");
                    break;
            }
            SetState();
        }


        /// <summary>
        /// Sets the state of the various UI elements based on the current state of the deployable part.
        /// Called whenever a significant event happens.
        /// </summary>
        private void SetState()
        {
            if (deployable == null)
            {
                // Something's set up wrong in config. Just turn off all UI for this mod.
                wakeTime = NOT_SLEEPING;
                SleepDisplayField.guiActive = false;
                SleepMinutesField.guiActive = false;
                SleepMinutesField.guiActiveEditor = false;
                SleepAction.active = false;
                SleepEvent.guiActive = false;
                return;
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                wakeTime = NOT_SLEEPING;
                SleepMinutesField.guiActiveEditor = true;
                SleepAction.active = true;
                SleepEvent.guiActive = false;
                return;
            }
            
            if (HighLogic.LoadedSceneIsFlight)
            {
                SleepDisplayField.guiActive = IsSleeping
                    && ((DeployState == ModuleDeployablePart.DeployState.RETRACTED)
                    || (DeployState == ModuleDeployablePart.DeployState.RETRACTING));
                bool isExtended = DeployState == ModuleDeployablePart.DeployState.EXTENDED;
                SleepEvent.guiActive = SleepMinutesField.guiActive = !IsSleeping && isExtended;
                SleepAction.active = true;

                if (IsSleeping)
                {
                    switch (DeployState)
                    {
                        case ModuleDeployablePart.DeployState.BROKEN:
                        case ModuleDeployablePart.DeployState.EXTENDED:
                        case ModuleDeployablePart.DeployState.EXTENDING:
                            Logging.Log("Sleeping " + Logging.ToString(part) + " is already " + DeployState + ", canceling sleep");
                            wakeTime = NOT_SLEEPING;
                            break;
                    }
                }
            }
        }

        private void UpdateSleepTimeRemaining()
        {
            int remaining = SleepSecondsRemaining;
            if (remaining == lastSleepSecondsRemaining) return;
            lastSleepSecondsRemaining = remaining;
            sleepDisplay = FormatSleepTimeDisplay();
        }

        /// <summary>
        /// Gets the number of sleep seconds remaining (0 if not currently asleep or past due to wake up).
        /// </summary>
        private int SleepSecondsRemaining
        {
            get
            {
                if (!IsSleeping) return 0;
                double now = Planetarium.GetUniversalTime();
                if (wakeTime <= now)
                {
                    // it's already past due!
                    return 0;
                }
                return (int)(0.99 + wakeTime - now);
            }
        }

        /// <summary>
        /// Gets the remaining sleep time, formatted as a string in mm:ss.
        /// </summary>
        /// <returns></returns>
        private string FormatSleepTimeDisplay()
        {
            int totalSeconds = SleepSecondsRemaining;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return string.Format("{0}:{1:00}", minutes, seconds);
        }

        /// <summary>
        /// Try to find the ModuleDeployablePart for the current part.
        /// </summary>
        /// <returns></returns>
        private ModuleDeployablePart FindDeployable()
        {
            if (part == null) return null;
            for (int i = 0; i < part.Modules.Count; ++i)
            {
                ModuleDeployablePart candidate = part.Modules[i] as ModuleDeployablePart;
                if (candidate != null) return candidate;
            }

            return null;
        }

        private class State
        {
            private readonly ModuleDeployableSleep module;
            private bool hasDeployable;
            private bool canMove;
            private bool isEditor;
            private bool isFlight;
            private bool isSleeping;
            private bool isOverdue;
            private ModuleDeployablePart.DeployState deployState;

            public State(ModuleDeployableSleep module)
            {
                this.module = module;
                Update();
            }

            public bool IsSameState(State other)
            {
                return (hasDeployable == other.hasDeployable)
                    && (canMove == other.canMove)
                    && (isEditor == other.isEditor)
                    && (isFlight == other.isFlight)
                    && (isSleeping == other.isSleeping)
                    && (isOverdue == other.isOverdue)
                    && (deployState == other.deployState);
            }

            public void Update()
            {
                hasDeployable = (module.deployable != null);
                canMove = module.deployable.CanMove;
                isEditor = HighLogic.LoadedSceneIsEditor;
                isFlight = HighLogic.LoadedSceneIsFlight;
                isSleeping = module.IsSleeping;
                isOverdue = isSleeping && (Planetarium.GetUniversalTime() > module.wakeTime);
                deployState = module.DeployState;
            }

            public bool IsOverdue { get { return isOverdue; } }

            public override string ToString()
            {
                return new StringBuilder()
                    .Append("hasDeployable=").Append(hasDeployable)
                    .Append(", canMove=").Append(canMove)
                    .Append(", isEditor=").Append(isEditor)
                    .Append(", isFlight=").Append(isFlight)
                    .Append(", isSleeping=").Append(isSleeping)
                    .Append(", isOverdue=").Append(isOverdue)
                    .Append(", deployState=").Append(deployState)
                    .ToString();
            }
        }
    }
}
