using HarmonyLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
#if UNHOLLOWER
using IL2CPPUtils = UnhollowerBaseLib.UnhollowerUtils;
#endif
#if INTEROP
using IL2CPPUtils = Il2CppInterop.Common.Il2CppInteropUtils;
#endif

namespace UnityExplorer.UI.Widgets
{
    internal class TimeScaleWidget
    {
        public TimeScaleWidget(GameObject parent)
        {
            Instance = this;

            ConstructUI(parent);

            InitPatch();
        }

        static TimeScaleWidget Instance;

        ButtonRef lockBtn;
        bool locked;
        InputFieldRef timeInput;
        float desiredTime;
        bool settingTimeScale;

        public void Update()
        {
            // Fallback in case Time.timeScale patch failed for whatever reason
            if (locked)
                SetTimeScale(desiredTime);

            if (!timeInput.Component.isFocused)
                timeInput.Text = Time.timeScale.ToString("F2");
        }

        void SetTimeScale(float time)
        {
            settingTimeScale = true;
            Time.timeScale = time;
            settingTimeScale = false;
        }

        // UI event listeners

        void OnTimeInputEndEdit(string val)
        {
            if (float.TryParse(val, out float f))
            {
                SetTimeScale(f);
                desiredTime = f;
            }
        }

        void OnPauseButtonClicked()
        {
            OnTimeInputEndEdit(timeInput.Text);

            locked = !locked;

            Color color = locked ? new Color(0.3f, 0.3f, 0.2f) : new Color(0.2f, 0.2f, 0.2f);
            RuntimeHelper.SetColorBlock(lockBtn.Component, color, color * 1.2f, color * 0.7f);
            lockBtn.ButtonText.text = locked ? "Unlock" : "Lock";
        }

        // Enable or disable game input
        public void LockInput()
        {
            // to enable game input => KSP.Game.GameManager.Instance.Game.Input.Enable();
            // to disable game input => KSP.Game.GameManager.Instance.Game.Input.Disable();
            // is game input enabled => KSP.Game.GameManager.Instance.Game.Input.m_Global.enabled

            // Get the currently executing assembly (your code).
            Assembly currentAssembly = Assembly.GetExecutingAssembly();

            // Find the "Assembly-CSharp" assembly among the loaded assemblies.
            Assembly targetAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");

            Type gameManagerType = targetAssembly.GetType("KSP.Game.GameManager");


            if (gameManagerType != null)
            {
                // Get the "Instance" property from KSP.Game.GameManager
                PropertyInfo instanceProperty = gameManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

                if (instanceProperty != null)
                {
                    // Get the value of the "Instance" property (an instance of KSP.Game.GameManager).
                    object gameManagerInstance = instanceProperty.GetValue(null, null);

                    // Get the "Game" property from the GameManager instance.
                    PropertyInfo gameProperty = gameManagerType.GetProperty("Game");

                    if (gameProperty != null)
                    {
                        // Get the value of the "Game" property (an instance of whatever type "Game" is).
                        object gameInstance = gameProperty.GetValue(gameManagerInstance, null);

                        // Get the "Input" property.
                        PropertyInfo inputProperty = gameInstance.GetType().GetProperty("Input");

                        if (inputProperty != null)
                        {
                            // Get the value of the "Input" property (an instance of whatever type "Input" is).
                            object inputInstance = inputProperty.GetValue(gameInstance, null);

                            //PropertyInfo globalProperty = inputInstance.GetType().GetProperty("m_Global", BindingFlags.NonPublic | BindingFlags.Instance);
                            FieldInfo globalField = inputInstance.GetType().GetField("m_Global", BindingFlags.NonPublic | BindingFlags.Instance);

                            if (globalField != null)
                            {
                                object globalInstance = globalField.GetValue(inputInstance);

                                PropertyInfo enabledProperty = globalInstance.GetType().GetProperty("enabled");

                                if (enabledProperty != null)
                                {
                                    bool enabled = (bool)enabledProperty.GetValue(globalInstance, null);

                                    // Get the Enable and Disable methods
                                    MethodInfo enableMethod = inputInstance.GetType().GetMethod("Enable");
                                    MethodInfo disableMethod = inputInstance.GetType().GetMethod("Disable");
                                    
                                    /// If it's needed to lock gameinput depending on current value =>
                                    //if (enabled)
                                    //    disableMethod?.Invoke(inputInstance, null);
                                    //else
                                    //    enableMethod?.Invoke(inputInstance, null);
                                    
                                    if (locked)
                                        disableMethod?.Invoke(inputInstance, null);
                                    else
                                        enableMethod?.Invoke(inputInstance, null);

                                }
                            }
                        }
                    }
                }
            }
        }

        // UI Construction

        void ConstructUI(GameObject parent)
        {
            Text timeLabel = UIFactory.CreateLabel(parent, "TimeLabel", "Time:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(timeLabel.gameObject, minHeight: 25, minWidth: 35);

            timeInput = UIFactory.CreateInputField(parent, "TimeInput", "timeScale");
            UIFactory.SetLayoutElement(timeInput.Component.gameObject, minHeight: 25, minWidth: 40);
            timeInput.Component.GetOnEndEdit().AddListener(OnTimeInputEndEdit);

            timeInput.Text = string.Empty;
            timeInput.Text = Time.timeScale.ToString();

            lockBtn = UIFactory.CreateButton(parent, "PauseButton", "Lock", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(lockBtn.Component.gameObject, minHeight: 25, minWidth: 50);
            lockBtn.OnClick += OnPauseButtonClicked;
            lockBtn.OnClick += LockInput;
        }

        // Only allow Time.timeScale to be set if the user hasn't "locked" it or if we are setting the value internally.

        static void InitPatch()
        {

            try
            {
                MethodInfo target = typeof(Time).GetProperty("timeScale").GetSetMethod();
#if CPP
                if (IL2CPPUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target) == null)
                    return;
#endif
                ExplorerCore.Harmony.Patch(target,
                    prefix: new(AccessTools.Method(typeof(TimeScaleWidget), nameof(Prefix_Time_set_timeScale))));
            }
            catch { }
        }

        static bool Prefix_Time_set_timeScale()
        {
            return !Instance.locked || Instance.settingTimeScale;
        }
    }
}
