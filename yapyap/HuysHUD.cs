using BepInEx;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
using Mirror;

namespace HuysHUD
{
    [BepInPlugin("com.huy.huyshud", "HuysHUD", "1.0.0")]
    public class MyPlugin : BaseUnityPlugin
    {
        // ── UI State ──────────────────────────────────────────────
        private bool showOverlay = false;
        private int  activeTab   = 0; // 0=Game, 1=Props

        // Game tab
        private int    gameFieldIndex = 0;
        private string[] gameFieldLabels = new string[]
        {
            "GameManager.gold", "GameManager.totalScore", "PawnBlackboard._stamina", "PawnBlackboard._maxStamina", "PawnBlackboard._staminaRegenRate", "Pawn.CDR%"
        };
        private string[] gameFieldValues = new string[] { "0","0","0","0","0","0" };
        private float[]  gameFieldSteps  = new float[]  { 11, 111, 10, 10, 0.5f, 5 };

        // Props tab
        private int propsCategory = 0; // 0=Potions, 1=Gold, 2=Wands
        private string[] propsCategoryLabels = new string[] { "Potions", "Gold", "Wands" };

        private string[] potionKeys    = new string[] { "proppotion_maxhp","proppotion_cdr","proppotion_staminaboost","proppotion_doublejump" };
        private string[] potionLabels  = new string[] { "Max HP","CDR","Stamina","Dbl Jump" };
        private int potionIndex = 0;

        private string[] goldKeys      = new string[] { "propgoldcoin","propgoldpile","propgoldbigpile","propgem" };
        private string[] goldLabels    = new string[] { "Coin","Pile","Big Pile","Gem" };
        private int goldIndex = 0;

        private int wandIndex = 0;

        // ── Styles ───────────────────────────────────────────────
        private GUIStyle styleWindow;
        private GUIStyle styleTab;
        private GUIStyle styleTabActive;
        private GUIStyle styleLabel;
        private GUIStyle styleLabelSelected;
        private GUIStyle styleField;
        private GUIStyle styleHint;
        private GUIStyle styleBtn;
        private GUIStyle styleBtnSelected;
        private bool stylesInit = false;

        // ── Reflection ───────────────────────────────────────────
        private System.Type gmType, pbType, pawnType;
        private FieldInfo goldField, scoreField, staminaField, maxStaminaField, regenField, cdrField, roundTimeElapsedField;
        private PropertyInfo gmInstanceProp;
        private FieldInfo localInstanceField;

        // ── Spawn ────────────────────────────────────────────────
        private Dictionary<string, UnityEngine.Object> itemCache = new Dictionary<string, UnityEngine.Object>();
        private List<string> wandNames = new List<string>();
        private bool itemsCached = false;

        // ── Input debounce ───────────────────────────────────────
        private float lastNavTime = 0f;
        private float navDelay = 0.18f;

        void Awake()
        {
            gmType = System.Type.GetType("YAPYAP.GameManager, Assembly-CSharp");
            if (gmType != null)
            {
                goldField             = gmType.GetField("gold",             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                scoreField            = gmType.GetField("totalScore",       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                gmInstanceProp        = gmType.GetProperty("Instance",      BindingFlags.Static   | BindingFlags.Public);
                roundTimeElapsedField = gmType.GetField("roundTimeElapsed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                localInstanceField = pawnType.GetField("LocalInstance",  BindingFlags.Static   | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // ── Getters/Setters ──────────────────────────────────────
        object GetGM()   { return gmInstanceProp   != null ? gmInstanceProp.GetValue(null, null) : null; }
        object GetPawn() { return localInstanceField != null ? localInstanceField.GetValue(null) : null; }
        object GetPB()
        {
            if (pbType == null) return null;
            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(pbType);
            PropertyInfo p = pbType.GetProperty("isLocalPlayer");
            foreach (var o in all) if (p != null && (bool)p.GetValue(o, null)) return o;
            return null;
        }

        float GetFieldValue(int idx)
        {
            switch (idx)
            {
                case 0: { object i = GetGM();   return (i!=null&&goldField  !=null)?(float)(int)goldField.GetValue(i)  :0; }
                case 1: { object i = GetGM();   return (i!=null&&scoreField !=null)?(float)(int)scoreField.GetValue(i) :0; }
                case 2: { object i = GetPB();   return (i!=null&&staminaField   !=null)?(float)staminaField.GetValue(i)   :0; }
                case 3: { object i = GetPB();   return (i!=null&&maxStaminaField!=null)?(float)maxStaminaField.GetValue(i) :0; }
                case 4: { object i = GetPB();   return (i!=null&&regenField     !=null)?(float)regenField.GetValue(i)     :0; }
                case 5: { object i = GetPawn(); return (i!=null&&cdrField!=null)?Mathf.Round((1f-(float)cdrField.GetValue(i))*100f):0; }
                default: return 0;
            }
        }

        void SetFieldValue(int idx, float val)
        {
            switch (idx)
            {
                case 0: { object i=GetGM();   if(i!=null&&goldField  !=null) goldField.SetValue(i,(int)Mathf.Max(0,val)); break; }
                case 1: { object i=GetGM();   if(i!=null&&scoreField !=null) scoreField.SetValue(i,(int)Mathf.Max(0,val)); break; }
                case 2: { object i=GetPB();   if(i!=null&&staminaField   !=null) staminaField.SetValue(i,val); break; }
                case 3: { object i=GetPB();   if(i!=null&&maxStaminaField!=null) maxStaminaField.SetValue(i,val); break; }
                case 4: { object i=GetPB();   if(i!=null&&regenField     !=null) regenField.SetValue(i,val); break; }
                case 5: { object i=GetPawn(); if(i!=null&&cdrField!=null) cdrField.SetValue(i,Mathf.Clamp(1f-(val/100f),0.05f,1f)); break; }
            }
        }

        void RefreshAllValues()
        {
            for (int i = 0; i < gameFieldLabels.Length; i++)
                gameFieldValues[i] = GetFieldValue(i).ToString("F1");
        }

        // ── Styles ───────────────────────────────────────────────
        Texture2D MakeTex(Color col)
        {
            Texture2D t = new Texture2D(1,1);
            t.SetPixel(0,0,col); t.Apply(); return t;
        }

        void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            styleLabel = new GUIStyle(GUI.skin.label);
            styleLabel.fontSize = 11;
            styleLabel.normal.textColor = new Color(0.85f,0.85f,0.85f);

            styleLabelSelected = new GUIStyle(styleLabel);
            styleLabelSelected.normal.textColor = new Color(1f,0.85f,0.3f);
            styleLabelSelected.fontStyle = FontStyle.Bold;

            styleField = new GUIStyle(GUI.skin.textField);
            styleField.fontSize = 11;

            styleHint = new GUIStyle(GUI.skin.label);
            styleHint.fontSize = 9;
            styleHint.normal.textColor = new Color(0.85f,0.85f,0.85f);

            styleTab = new GUIStyle(GUI.skin.button);
            styleTab.fontSize = 11;
            styleTab.normal.textColor = new Color(0.9f,0.9f,0.9f);

            styleTabActive = new GUIStyle(styleTab);
            styleTabActive.normal.textColor = Color.white;
            styleTabActive.fontStyle = FontStyle.Bold;
            styleTabActive.normal.background = MakeTex(new Color(0.25f,0.25f,0.45f,1f));

            styleBtn = new GUIStyle(GUI.skin.button);
            styleBtn.fontSize = 11;

            styleBtnSelected = new GUIStyle(styleBtn);
            styleBtnSelected.normal.textColor = new Color(1f,0.85f,0.3f);
            styleBtnSelected.fontStyle = FontStyle.Bold;
            styleBtnSelected.normal.background = MakeTex(new Color(0.3f,0.3f,0.15f,1f));
        }

        // ── Spawn ────────────────────────────────────────────────
        void CacheItems()
        {
            itemCache.Clear(); wandNames.Clear();
            Assembly asm = Assembly.Load("Assembly-CSharp");
            System.Type valType=null, puppetType=null, wandType=null;
            foreach (System.Type t in asm.GetTypes())
            {
                if (t.FullName=="YAPYAP.ValuableObject")        valType    = t;
                if (t.FullName=="YAPYAP.NetworkPuppetProp")     puppetType = t;
                if (t.FullName=="YAPYAP.NetworkPuppetWandProp") wandType   = t;
            }
            foreach (System.Type type in new System.Type[]{valType,puppetType})
            {
                if (type==null) continue;
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(type))
                {
                    if (obj==null) continue;
                    GameObject go = obj as GameObject;
                    if (go==null){Component c=obj as Component;if(c!=null)go=c.gameObject;}
                    if (go!=null&&go.scene.name==null){string k=go.name.ToLower();if(!itemCache.ContainsKey(k))itemCache[k]=obj;}
                }
            }
            if (wandType!=null)
            {
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(wandType))
                {
                    if (obj==null) continue;
                    GameObject go = obj as GameObject;
                    if (go==null){Component c=obj as Component;if(c!=null)go=c.gameObject;}
                    if (go!=null&&go.scene.name==null){string k=go.name.ToLower();wandNames.Add(go.name);if(!itemCache.ContainsKey(k))itemCache[k]=obj;}
                }
            }
            itemsCached = true;
        }

        void SpawnItem(string searchKey)
        {
            try
            {
                if (!itemsCached) CacheItems();
                object pawn = GetPawn();
                if (pawn==null) return;
                PropertyInfo tp = pawn.GetType().GetProperty("transform", BindingFlags.Instance|BindingFlags.Public);
                Transform tr = tp!=null?(Transform)tp.GetValue(pawn,null):null;
                if (tr==null) return;
                Vector3 pos = tr.position + tr.forward*2f + Vector3.up*0.5f;
                string key = searchKey.ToLower();
                UnityEngine.Object found = null;
                foreach (var kvp in itemCache) if(kvp.Key.Contains(key)){found=kvp.Value;break;}
                if (found==null) return;
                GameObject go = found as GameObject;
                if (go==null){Component c=found as Component;if(c!=null)go=c.gameObject;}
                if (go==null) return;
                NetworkIdentity nid = go.GetComponent<NetworkIdentity>();
                if (nid==null) return;
                object gm = GetGM();
                if (gm==null) return;
                MethodInfo m = gm.GetType().GetMethod("SpawnNetworkPrefab",BindingFlags.Static|BindingFlags.Public,null,
                    new System.Type[]{typeof(NetworkIdentity),typeof(Vector3),typeof(Quaternion),typeof(bool)},null);
                if (m!=null) m.Invoke(null,new object[]{nid,pos,Quaternion.identity,true});
            }
            catch(Exception ex){Logger.LogError("Spawn: "+ex.Message);}
        }

        // ── Update ───────────────────────────────────────────────
        void Update()
        {
            // Toggle HUD
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                showOverlay = !showOverlay;
                if (showOverlay) { RefreshAllValues(); if(!itemsCached)CacheItems(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
                return;
            }
            if (!showOverlay) return;

            bool canNav = Time.unscaledTime - lastNavTime > navDelay;

            // Switch tabs
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                activeTab = (activeTab+1)%2;
                lastNavTime = Time.unscaledTime;
                return;
            }

            if (activeTab == 0) HandleGameTab(canNav);
            else                HandlePropsTab(canNav);

            // Numpad Enter = confirm always
            if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (activeTab==1) SpawnCurrentProp();
            }
        }

        void HandleGameTab(bool canNav)
        {
            // Cycle fields
            if (canNav && (Input.GetKey(KeyCode.Keypad4)||Input.GetKey(KeyCode.Keypad6)))
            {
                int dir = Input.GetKey(KeyCode.Keypad6) ? 1 : -1;
                gameFieldIndex = (gameFieldIndex+dir+gameFieldLabels.Length)%gameFieldLabels.Length;
                lastNavTime = Time.unscaledTime;
            }
            // Increase/decrease
            if (canNav && (Input.GetKey(KeyCode.KeypadPlus)||Input.GetKey(KeyCode.KeypadMinus)))
            {
                float dir = Input.GetKey(KeyCode.KeypadPlus) ? 1f : -1f;
                float cur = GetFieldValue(gameFieldIndex);
                SetFieldValue(gameFieldIndex, cur + dir*gameFieldSteps[gameFieldIndex]);
                gameFieldValues[gameFieldIndex] = GetFieldValue(gameFieldIndex).ToString("F1");
                lastNavTime = Time.unscaledTime;
            }
            // Round time bonus
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                object i = GetGM();
                if (i!=null&&roundTimeElapsedField!=null)
                    roundTimeElapsedField.SetValue(i, Mathf.Max(0f,(float)roundTimeElapsedField.GetValue(i)-30f));
            }
        }

        void HandlePropsTab(bool canNav)
        {
            // Cycle category
            if (canNav && (Input.GetKey(KeyCode.Keypad4)||Input.GetKey(KeyCode.Keypad6)))
            {
                int dir = Input.GetKey(KeyCode.Keypad6) ? 1 : -1;
                propsCategory = (propsCategory+dir+3)%3;
                lastNavTime = Time.unscaledTime;
            }
            // Cycle item within category
            if (canNav && (Input.GetKey(KeyCode.Keypad8)||Input.GetKey(KeyCode.Keypad2)))
            {
                bool fwd = Input.GetKey(KeyCode.Keypad2);
                int dir = fwd ? 1 : -1;
                if (propsCategory==0) potionIndex=(potionIndex+dir+potionKeys.Length)%potionKeys.Length;
                else if (propsCategory==1) goldIndex=(goldIndex+dir+goldKeys.Length)%goldKeys.Length;
                else if (propsCategory==2 && wandNames.Count>0) wandIndex=(wandIndex+dir+wandNames.Count)%wandNames.Count;
                lastNavTime = Time.unscaledTime;
            }
        }

        void SpawnCurrentProp()
        {
            if (propsCategory==0) SpawnItem(potionKeys[potionIndex]);
            else if (propsCategory==1) SpawnItem(goldKeys[goldIndex]);
            else if (propsCategory==2 && wandNames.Count>0) SpawnItem(wandNames[wandIndex]);
        }

        // ── GUI ──────────────────────────────────────────────────
        void OnGUI()
        {
            if (!showOverlay) return;
            InitStyles();

            float w = 340f;
            float x = (Screen.width-w)/2f;
            float y = Screen.height - 230f;

            Color prev = GUI.color;
            GUI.color = new Color(1f,1f,1f,0.95f);
            GUI.Window(9999, new Rect(x,y,w,220f), DrawWindow, "Huy's HUD");
            GUI.color = prev;
        }

        void DrawWindow(int id)
        {
            // Tab bar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Game",  activeTab==0?styleTabActive:styleTab, GUILayout.Height(22))) activeTab=0;
            if (GUILayout.Button("Props", activeTab==1?styleTabActive:styleTab, GUILayout.Height(22))) activeTab=1;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (activeTab==0) DrawGameTab();
            else              DrawPropsTab();

            GUILayout.Space(4);
            GUILayout.Label("Num* toggle  |  Tab switch  |  Num- +30s round", styleHint);
            GUI.DragWindow();
        }

        void DrawGameTab()
        {
            GUILayout.Label("Num4/6 cycle field  |  Num+/- change value", styleHint);
            GUILayout.Space(2);
            for (int i=0; i<gameFieldLabels.Length; i++)
            {
                bool sel = i==gameFieldIndex;
                GUILayout.BeginHorizontal();
                string arrow = sel ? "▶ " : "   ";
                GUILayout.Label(arrow+gameFieldLabels[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(200));
                GUILayout.Label(gameFieldValues[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(80));
                GUILayout.Label("±"+gameFieldSteps[i].ToString("G"), styleHint, GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
        }

        void DrawPropsTab()
        {
            // Category tabs
            GUILayout.BeginHorizontal();
            for (int i=0; i<propsCategoryLabels.Length; i++)
            {
                bool sel = i==propsCategory;
                if (GUILayout.Button(propsCategoryLabels[i], sel?styleTabActive:styleTab, GUILayout.Height(20)))
                    propsCategory=i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Num4/6 cycle category  |  ←/→ or Num8/2 cycle item  |  NumEnter spawn", styleHint);
            GUILayout.Space(4);

            if (propsCategory==0)
            {
                GUILayout.Label("Selected potion:", styleLabel);
                GUILayout.BeginHorizontal();
                for (int i=0; i<potionLabels.Length; i++)
                    GUILayout.Label((i==potionIndex?"▶ ":"   ")+potionLabels[i], i==potionIndex?styleLabelSelected:styleLabel);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                if (GUILayout.Button("Spawn: "+potionLabels[potionIndex], styleBtn)) SpawnItem(potionKeys[potionIndex]);
            }
            else if (propsCategory==1)
            {
                GUILayout.Label("Selected gold:", styleLabel);
                GUILayout.BeginHorizontal();
                for (int i=0; i<goldLabels.Length; i++)
                    GUILayout.Label((i==goldIndex?"▶ ":"   ")+goldLabels[i], i==goldIndex?styleLabelSelected:styleLabel);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                if (GUILayout.Button("Spawn: "+goldLabels[goldIndex], styleBtn)) SpawnItem(goldKeys[goldIndex]);
            }
            else if (propsCategory==2)
            {
                if (wandNames.Count==0) { GUILayout.Label("No wands cached", styleLabel); return; }
                GUILayout.Label("Num8/2 or ↑/↓ to cycle wands:", styleLabel);
                int start = Mathf.Max(0, wandIndex-2);
                int end   = Mathf.Min(wandNames.Count-1, start+4);
                for (int i=start; i<=end; i++)
                {
                    bool sel = i==wandIndex;
                    GUILayout.Label((sel?"▶ ":"   ")+wandNames[i], sel?styleLabelSelected:styleLabel);
                }
                GUILayout.Space(4);
                if (GUILayout.Button("Spawn: "+wandNames[wandIndex], styleBtn)) SpawnItem(wandNames[wandIndex]);
            }
        }
    }
}