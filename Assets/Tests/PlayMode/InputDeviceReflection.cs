using System;
using System.Linq;
using System.Reflection;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// Adds a test Keyboard and queues state events through the Input System's public API, called via
    /// reflection because RuralRumble.Tests.PlayMode.asmdef does not reference Unity.InputSystem (the
    /// asmdef is Architect-owned; the missing reference is reported). Replace this with direct calls
    /// (InputSystem.AddDevice&lt;Keyboard&gt;(), QueueStateEvent(new KeyboardState(Key.D))) once
    /// the reference is added.
    ///
    /// It also makes keyboard events reach the game even when the Game view does not have focus (test runs
    /// launched from the CLI), and restores those settings and removes the device on Dispose.
    /// </summary>
    public sealed class TestKeyboard : IDisposable
    {
        private static readonly Assembly InputAsm =
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.InputSystem");

        private readonly Type inputSystem;
        private readonly Type keyType;
        private readonly Type keyboardStateType;
        private readonly object device;
        private readonly object settings;
        private readonly PropertyInfo backgroundProp;
        private readonly PropertyInfo editorBehaviorProp;
        private readonly object oldBackground;
        private readonly object oldEditorBehavior;

        public TestKeyboard()
        {
            if (InputAsm == null)
                throw new InvalidOperationException("Unity.InputSystem assembly is not loaded.");
            inputSystem = InputAsm.GetType("UnityEngine.InputSystem.InputSystem", true);
            keyType = InputAsm.GetType("UnityEngine.InputSystem.Key", true);
            keyboardStateType = InputAsm.GetType("UnityEngine.InputSystem.LowLevel.KeyboardState", true);

            settings = inputSystem.GetProperty("settings", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Type settingsType = settings.GetType();
            backgroundProp = settingsType.GetProperty("backgroundBehavior");
            editorBehaviorProp = settingsType.GetProperty("editorInputBehaviorInPlayMode");
            oldBackground = backgroundProp?.GetValue(settings);
            oldEditorBehavior = editorBehaviorProp?.GetValue(settings);
            if (backgroundProp != null)
                backgroundProp.SetValue(settings, Enum.Parse(backgroundProp.PropertyType, "IgnoreFocus"));
            if (editorBehaviorProp != null)
                editorBehaviorProp.SetValue(settings, Enum.Parse(editorBehaviorProp.PropertyType, "AllDeviceInputAlwaysGoesToGameView"));

            MethodInfo addDevice = inputSystem.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m =>
                m.Name == "AddDevice" && !m.IsGenericMethod && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType == typeof(string));
            device = addDevice.Invoke(null, new object[] { "Keyboard", "QA Test Keyboard", null });
        }

        /// <summary>Queues a full keyboard state where exactly these keys are down (Key enum names, e.g. "D", "Space").</summary>
        public void SetPressed(params string[] keyNames)
        {
            Array keys = Array.CreateInstance(keyType, keyNames.Length);
            for (int i = 0; i < keyNames.Length; i++)
                keys.SetValue(Enum.Parse(keyType, keyNames[i]), i);
            ConstructorInfo ctor = keyboardStateType.GetConstructor(new[] { keyType.MakeArrayType() });
            object state = ctor.Invoke(new object[] { keys });

            MethodInfo queue = inputSystem.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "QueueStateEvent" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(keyboardStateType);
            queue.Invoke(null, new[] { device, state, -1.0 });
        }

        public void ReleaseAll() => SetPressed();

        public void Dispose()
        {
            try { ReleaseAll(); } catch { /* device may already be gone */ }
            MethodInfo remove = inputSystem.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m =>
                m.Name == "RemoveDevice" && m.GetParameters().Length == 1);
            remove.Invoke(null, new[] { device });
            if (backgroundProp != null) backgroundProp.SetValue(settings, oldBackground);
            if (editorBehaviorProp != null) editorBehaviorProp.SetValue(settings, oldEditorBehavior);
        }
    }
}
