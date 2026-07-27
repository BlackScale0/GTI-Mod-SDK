using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.Mono;
using UnityEngine;

namespace ISTReflectionExample
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.ist.reflectionexample";
        public const string PluginName    = "IST Reflection Example";
        public const string PluginVersion = "1.0.0";

        // ClientOnly: only affects local movement, no sync needed. See README section 5.
        public const string ISTCompat = "ClientOnly";

        private const KeyCode ToggleKey      = KeyCode.F1;
        private const float   SpeedMultiplier = 4f;

        private bool _speedEnabled;

        // Resolved via reflection at startup so no hard Assembly-CSharp reference is needed.
        private Type         _playerControllerType;
        private FieldInfo    _walkSpeedField;
        private FieldInfo    _runSpeedField;
        private FieldInfo    _sprintSpeedField;
        private PropertyInfo _isLocalPlayerProp;

        // Original speeds per player instance, used to restore on toggle off.
        private readonly Dictionary<object, float[]> _originalSpeeds = new Dictionary<object, float[]>();

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. Press {ToggleKey} to toggle speed boost.");
            ResolveReflection();
        }

        private void ResolveReflection()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { _playerControllerType = asm.GetType("PlayerController", false); }
                catch { continue; }
                if (_playerControllerType != null) break;
            }

            if (_playerControllerType == null)
            {
                Logger.LogWarning("Could not find type 'PlayerController'. Speed boost disabled.");
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _walkSpeedField    = _playerControllerType.GetField("_walkSpeed",   flags);
            _runSpeedField     = _playerControllerType.GetField("_runSpeed",    flags);
            _sprintSpeedField  = _playerControllerType.GetField("_sprintSpeed", flags);
            // isLocalPlayer is a Mirror NetworkBehaviour property.
            _isLocalPlayerProp = _playerControllerType.GetProperty("isLocalPlayer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _speedEnabled = !_speedEnabled;
                Logger.LogInfo($"Speed boost {(_speedEnabled ? "enabled" : "disabled")}.");

                if (_speedEnabled)
                    ApplySpeedBoost();
                else
                    RestoreAllSpeeds();
            }
        }

        private void ApplySpeedBoost()
        {
            if (_playerControllerType == null || _sprintSpeedField == null) return;

            foreach (object pc in FindLocalPlayers())
            {
                if (!_originalSpeeds.ContainsKey(pc))
                {
                    _originalSpeeds[pc] = new[]
                    {
                        (float)_walkSpeedField.GetValue(pc),
                        (float)_runSpeedField.GetValue(pc),
                        (float)_sprintSpeedField.GetValue(pc)
                    };
                }

                float[] base_ = _originalSpeeds[pc];
                _walkSpeedField.SetValue(pc,   base_[0] * SpeedMultiplier);
                _runSpeedField.SetValue(pc,    base_[1] * SpeedMultiplier);
                _sprintSpeedField.SetValue(pc, base_[2] * SpeedMultiplier);
            }
        }

        private void RestoreAllSpeeds()
        {
            foreach (KeyValuePair<object, float[]> kv in _originalSpeeds)
            {
                object pc = kv.Key;
                if (pc == null) continue;
                try
                {
                    _walkSpeedField.SetValue(pc,   kv.Value[0]);
                    _runSpeedField.SetValue(pc,    kv.Value[1]);
                    _sprintSpeedField.SetValue(pc, kv.Value[2]);
                }
                catch { /* player was destroyed */ }
            }
            _originalSpeeds.Clear();
        }

        private IEnumerable<object> FindLocalPlayers()
        {
            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(_playerControllerType);
            foreach (UnityEngine.Object obj in all)
            {
                bool isLocal = _isLocalPlayerProp != null && (bool)_isLocalPlayerProp.GetValue(obj, null);
                if (isLocal)
                    yield return obj;
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold };
            style.normal.textColor = _speedEnabled ? Color.green : Color.white;

            string status = _speedEnabled ? $"Movement speed: x{SpeedMultiplier}" : "Speed boost: off";
            GUI.Label(new Rect(20, 20, 600, 40), $"[{PluginName}]  {status}  ({ToggleKey})", style);
        }
    }
}