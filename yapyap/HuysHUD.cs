using BepInEx;
using UnityEngine;
using System.Reflection;

namespace YapYapMod
{
    [BepInPlugin("com.you.yapyapmod", "YapYap Mod", "1.0.0")]
    public class MyPlugin : BaseUnityPlugin
    {
        private bool showOverlay = false;
        private string goldInput = "0";
        private string scoreInput = "0";
        private string staminaInput = "0";
        private string maxStaminaInput = "0";
        private string regenInput = "0";
        private string cdrInput = "0";
        private Rect windowRect = new Rect(0, 0, 330, 130);
        private GUIStyle smallLabel;
        private GUIStyle smallField;
        private GUIStyle smallHint;
        private bool stylesInit = false;

        // GameManager reflection
        private System.Type gmType;
        private FieldInfo goldField;
        private FieldInfo scoreField;
        private PropertyInfo gmInstanceProp;

        // PawnBlackboard reflection
        private System.Type pbType;
        private FieldInfo staminaField;
        private FieldInfo maxStaminaField;
        private FieldInfo regenField;

        // Pawn reflection
        private System.Type pawnType;
        private FieldInfo cdrField;
        private FieldInfo localInstanceField;

        // Round time
        private FieldInfo roundTimeElapsedField;

        void Awake()
        {
            gmType = System.Type.GetType("YAPYAP.GameManager, Assembly-CSharp");
            if (gmType != null)
            {
                goldField      = gmType.GetField("gold",       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                scoreField     = gmType.GetField("totalScore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                gmInstanceProp = gmType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            }

            pbType = System.Type.GetType("YAPYAP.PawnBlackboard, Assembly-CSharp");
            if (pbType != null)
            {
                staminaField    = pbType.GetField("_stamina",          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                maxStaminaField = pbType.GetField("_maxStamina",       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                regenField      = pbType.GetField("_staminaRegenRate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            pawnType = System.Type.GetType("YAPYAP.Pawn, Assembly-CSharp");
            if (pawnType != null)
            {
                cdrField           = pawnType.GetField("_cdrMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                localInstanceField = pawnType.GetField("LocalInstance",  BindingFlags.Static  | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (gmType != null)
                roundTimeElapsedField = gmType.GetField("roundTimeElapsed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;
            smallLabel = new GUIStyle(GUI.skin.label);
            smallLabel.fontSize = 10;
            smallLabel.padding = new RectOffset(2, 2, 2, 0);
            smallField = new GUIStyle(GUI.skin.textField);
            smallField.fontSize = 10;
            smallField.padding = new RectOffset(2, 2, 2, 0);
            smallHint = new GUIStyle(GUI.skin.label);
            smallHint.fontSize = 9;
            smallHint.normal.textColor = smallLabel.normal.textColor;
            smallHint.padding = new RectOffset(2, 2, 2, 0);
        }

        object GetGMInstance()   { return gmInstanceProp != null ? gmInstanceProp.GetValue(null, null) : null; }
        object GetPawnInstance() { return localInstanceField != null ? localInstanceField.GetValue(null) : null; }
        object GetPBInstance()
        {
            if (pbType == null) return null;
            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(pbType);
            PropertyInfo isLocal = pbType.GetProperty("isLocalPlayer");
            foreach (var obj in all)
                if (isLocal != null && (bool)isLocal.GetValue(obj, null)) return obj;
            return null;
        }

        int   GetGold()  { object i = GetGMInstance();   return (i != null && goldField  != null) ? (int)goldField.GetValue(i)   : 0; }
        void  SetGold(int v)   { object i = GetGMInstance();   if (i != null && goldField  != null) goldField.SetValue(i, v); }
        int   GetScore() { object i = GetGMInstance();   return (i != null && scoreField != null) ? (int)scoreField.GetValue(i)  : 0; }
        void  SetScore(int v)  { object i = GetGMInstance();   if (i != null && scoreField != null) scoreField.SetValue(i, v); }
        float GetStamina()     { object i = GetPBInstance();   return (i != null && staminaField    != null) ? (float)staminaField.GetValue(i)    : 0f; }
        void  SetStamina(float v)    { object i = GetPBInstance();   if (i != null && staminaField    != null) staminaField.SetValue(i, v); }
        float GetMaxStamina()  { object i = GetPBInstance();   return (i != null && maxStaminaField != null) ? (float)maxStaminaField.GetValue(i) : 0f; }
        void  SetMaxStamina(float v) { object i = GetPBInstance();   if (i != null && maxStaminaField != null) maxStaminaField.SetValue(i, v); }
        float GetRegen()       { object i = GetPBInstance();   return (i != null && regenField      != null) ? (float)regenField.GetValue(i)      : 0f; }
        void  SetRegen(float v)      { object i = GetPBInstance();   if (i != null && regenField      != null) regenField.SetValue(i, v); }
        float GetCdr()         { object i = GetPawnInstance(); return (i != null && cdrField != null) ? (float)cdrField.GetValue(i) : 1f; }
        void  SetCdr(float v)        { object i = GetPawnInstance(); if (i != null && cdrField != null) cdrField.SetValue(i, Mathf.Clamp(v, 0.05f, 1f)); }

        float GetRoundTimeElapsed() { object i = GetGMInstance(); return (i != null && roundTimeElapsedField != null) ? (float)roundTimeElapsedField.GetValue(i) : 0f; }
        void SetRoundTimeElapsed(float v) { object i = GetGMInstance(); if (i != null && roundTimeElapsedField != null) roundTimeElapsedField.SetValue(i, Mathf.Max(0f, v)); }

        float CdrToPercent(float cdr) { return Mathf.Round((1f - cdr) * 100f); }
        float PercentToCdr(float pct) { return 1f - (pct / 100f); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                showOverlay = !showOverlay;
                if (showOverlay) { RefreshValues(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            if (Input.GetKeyDown(KeyCode.Keypad2)) { SetGold(GetGold() + 11);  goldInput  = GetGold().ToString(); }
            if (Input.GetKeyDown(KeyCode.Keypad1)) { SetGold(Mathf.Max(0, GetGold() - 11)); goldInput = GetGold().ToString(); }
            if (Input.GetKeyDown(KeyCode.Keypad4)) { SetScore(Mathf.Max(0, GetScore() - 111)); scoreInput = GetScore().ToString(); }
            if (Input.GetKeyDown(KeyCode.Keypad5)) { SetScore(GetScore() + 111); scoreInput = GetScore().ToString(); }
            if (Input.GetKeyDown(KeyCode.Keypad7)) { SetCdr(GetCdr() + 0.05f); cdrInput = CdrToPercent(GetCdr()).ToString("F0"); }
            if (Input.GetKeyDown(KeyCode.KeypadMinus)) { SetRoundTimeElapsed(GetRoundTimeElapsed() - 30f); }
            if (Input.GetKeyDown(KeyCode.Keypad8)) { SetCdr(GetCdr() - 0.05f); cdrInput = CdrToPercent(GetCdr()).ToString("F0"); }
            if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                int gval;    if (int.TryParse(goldInput,       out gval))  SetGold(gval);
                int sval;    if (int.TryParse(scoreInput,      out sval))  SetScore(sval);
                float stval; if (float.TryParse(staminaInput,    out stval)) SetStamina(stval);
                float mval;  if (float.TryParse(maxStaminaInput, out mval))  SetMaxStamina(mval);
                float rval;  if (float.TryParse(regenInput,      out rval))  SetRegen(rval);
                float cval;  if (float.TryParse(cdrInput,        out cval))  SetCdr(PercentToCdr(cval));
            }
        }

        void RefreshValues()
        {
            goldInput       = GetGold().ToString();
            scoreInput      = GetScore().ToString();
            staminaInput    = GetStamina().ToString("F1");
            maxStaminaInput = GetMaxStamina().ToString("F1");
            regenInput      = GetRegen().ToString("F2");
            cdrInput        = CdrToPercent(GetCdr()).ToString("F0");
        }

        void OnGUI()
        {
            if (!showOverlay) return;
            InitStyles();
            windowRect.x = (Screen.width - windowRect.width) / 2f;
            windowRect.y = Screen.height - windowRect.height - 10f;
            windowRect = GUI.Window(9999, windowRect, DrawWindow, "Huy's HUD");
        }

        void Row(string label, string hint, ref string input)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, smallLabel, GUILayout.Width(195));
            input = GUILayout.TextField(input, smallField, GUILayout.Width(55));
            GUILayout.Label(hint, smallHint, GUILayout.Width(35));
            GUILayout.EndHorizontal();
        }

        void DrawWindow(int id)
        {
            Row("GameManager.gold",              "Num0",   ref goldInput);
            Row("GameManager.totalScore",        "Num1",   ref scoreInput);
            Row("PawnBlackboard._stamina",       "Enter",  ref staminaInput);
            Row("PawnBlackboard._maxStamina",    "Enter",  ref maxStaminaInput);
            Row("PawnBlackboard._staminaRegenRate", "Enter", ref regenInput);
            Row("Pawn.CDR%",                     "Num+/-", ref cdrInput);
            GUI.DragWindow();
        }
    }
}