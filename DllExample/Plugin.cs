using BepInEx;
using BepInEx.Unity.Mono;
using UnityEngine;

namespace ISTDllExample
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.ist.dllexample";
        public const string PluginName    = "IST DLL Example";
        public const string PluginVersion = "1.0.0";

        // ClientOnly: only affects local movement, no sync needed. See README section 5.
        public const string ISTCompat = "ClientOnly";

        private const KeyCode ToggleKey       = KeyCode.F1;
        private const float   SpeedMultiplier = 4f;

        private bool             _speedEnabled;
        private PlayerController _localPlayer;

        // Original speeds stored on toggle-on so we can restore them on toggle-off.
        private float _origWalk;
        private float _origRun;
        private float _origSprint;

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. Press {ToggleKey} to toggle speed boost.");
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
                    RestoreSpeeds();
            }
        }

        private PlayerController GetLocalPlayer()
        {
            // Mirror sets isLocalPlayer on the object owned by this client.
            foreach (PlayerController pc in FindObjectsOfType<PlayerController>())
            {
                if (pc.isLocalPlayer)
                    return pc;
            }
            return null;
        }

        private void ApplySpeedBoost()
        {
            _localPlayer = GetLocalPlayer();
            if (_localPlayer == null) return;

            _origWalk   = _localPlayer._walkSpeed;
            _origRun    = _localPlayer._runSpeed;
            _origSprint = _localPlayer._sprintSpeed;

            _localPlayer._walkSpeed   = _origWalk   * SpeedMultiplier;
            _localPlayer._runSpeed    = _origRun    * SpeedMultiplier;
            _localPlayer._sprintSpeed = _origSprint * SpeedMultiplier;
        }

        private void RestoreSpeeds()
        {
            if (_localPlayer == null) return;

            _localPlayer._walkSpeed   = _origWalk;
            _localPlayer._runSpeed    = _origRun;
            _localPlayer._sprintSpeed = _origSprint;
            _localPlayer = null;
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